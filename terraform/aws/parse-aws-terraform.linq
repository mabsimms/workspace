<Query Kind="Program">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
</Query>

void Main()
{
	var file = @"C:\code\workspace\terraform\logs\aws\simple-vm.log";
	var index = 0;
	var contents = File.ReadAllLines(file)
		.Where(f => f.StartsWith("2018") && f.Contains(": "))		
		.Select(f => new TerraformRecord
		{
			Index = index++,
			Timestamp = DateTime.Parse(f.Substring(0, f.IndexOf('[') - 1)),
			Level = f.Substring(f.IndexOf('[') + 1, f.IndexOf(']') - f.IndexOf('[') - 1),
			Message = f.Substring(f.IndexOf(']') + 2)
		})
		.ToArray()
	;

	var apiCalls = contents
	    .Where(c => c.Message.Contains("---[ REQUEST POST-SIGN ]-----------------------------"))
	    .Select(c => GetAwsRequest(contents, c))		
	    .ToArray();
	apiCalls = ConsolidateDescribeOperations(apiCalls);
	
	var graph = CreateGraph(apiCalls.ToArray());
	Console.WriteLine(
		JsonConvert.SerializeObject(graph));
		
	//	var response = GetAwsRequest(contents, apiCalls[0]);
//	response.Dump("response");
}

public AwsRequest[] ConsolidateDescribeOperations(AwsRequest[] requests)
{
	// Pull out all of the describe operation requests
	var checks = requests.Where(r => r.Action == "DescribeInstances")
		.GroupBy(r => r.ResourceId);
		
	// TODO - this is clearly going to breawk for any non trivial patterns
	var calls  = requests.Where(r => r.Action != "DescribeInstances" && r.Action != "RunInstances"); 
	var runs   = requests.Where(r => r.Action == "RunInstances").ToDictionary(r => r.ResourceId, v => v ); 
	//requests.Dump("requests");
	//runs.Dump("runs");
	
	foreach (var c in checks)
	{
		//Console.WriteLine("Checking for instance " + c.Key);
		
		if (runs.ContainsKey(c.Key))
		{
			var call = runs[c.Key];
			call.SubRequests = c.ToArray();
			call.ResponseTimestamp = call.SubRequests.Max(e => e.ResponseTimestamp);
			call.Elapsed = call.ResponseTimestamp.Subtract(call.RequestTimestamp);
		}
		else
		{
			Console.WriteLine("runs does not contain key " + c.Key);	
		}
	}
	
	var all = runs.Select(r => r.Value).Union(calls).OrderBy(r => r.RequestTimestamp ).ToArray();
	return all;
}

public Graph CreateGraph(AwsRequest[] apis)
{
	// Add the primary operations
	var traces = new List<Trace>();
	var relations = new List<Relationship>();

	// Add the sub operations
	int traceIndex = 1;
	foreach (var api in apis)
	{		
		var trace = new Trace
		{
			id = traceIndex++,
			name = api.Action + "/" + api.ResourceId,
			resultType = "SUCCESS",
			hidden = false,
			systemHidden = false,
			startNanos = api.RequestTimestamp.Ticks * 100,
			endNanos = api.ResponseTimestamp.Ticks * 100
		};
		traces.Add(trace);

		var relation = new Relationship()
		{
			relationship = "PARENT_OF",
			from = 0,
			to = trace.id
		};
		relations.Add(relation);

		if (api.SubRequests != null && api.SubRequests.Length > 0)
		{
			var subTraces = api.SubRequests.Select(oc => new Trace()
			{
				id = traceIndex++,
				name = api.Action + "/" + api.ResourceId,
				resultType = "SUCCESS",
				hidden = false,
				systemHidden = false,
				startNanos = oc.RequestTimestamp.Ticks * 100,
				endNanos = oc.ResponseTimestamp.Ticks * 100
			}).ToArray();

			traces.AddRange(subTraces);

			var subRelations = subTraces.Select(t => new Relationship()
			{
				relationship = "PARENT_OF",
				from = trace.id,
				to = t.id
			});

			relations.AddRange(subRelations);
		}
	}

	traces.Insert(0, new Trace()
	{
		id = 0,
		name = "CreateVM",
		resultType = "SUCCESS",
		hidden = false,
		systemHidden = false,
		startNanos = traces.Min(t => t.startNanos),
		endNanos = traces.Max(t => t.endNanos)
	});


	var graph = new Graph
	{
		traces = traces.ToArray(),
		relationships = relations.ToArray()
	};
	return graph;
}

public AwsRequest GetAwsRequest(TerraformRecord[] contents, TerraformRecord apiCall)
{
	// Find the block of request (next block of ---------------) after the request
	var requestBlock = contents.Skip(apiCall.Index).TakeWhile(c => !c.Message.Contains(": --------------") ).ToArray();
	
	// The POST content should be in the final element
	var postBody = requestBlock.Last();
	var actionMessage = postBody.Message.Substring(postBody.Message.IndexOf(": ")+2);
	var actionSegments = actionMessage.Split('&').ToDictionary(a => a.Split('=')[0], b => b.Split('=')[1]);
	var action = actionSegments.ContainsKey("Action") ? actionSegments["Action"] : "Generic";	
	//actionSegments.Dump("actions");
	
	// Find the response immediately after the request (this will likely break for more complicated terraform apis)
	var responseStart = contents.Skip(postBody.Index).SkipWhile(c => !c.Message.Contains("---[ RESPONSE ]" )).Take(1).First();
	var headerBlock = contents.Skip(responseStart.Index).TakeWhile(c => !c.Message.Contains(": --------------") ).ToArray();
	//headerBlock.Dump("header");
	
	var contentBlock = contents.Skip(headerBlock.Last().Index+2).TakeWhile(c => !c.Message.Contains(": ---") ).ToArray();
	//contentBlock.Dump("content");
	


	var resourceIdMap = new Dictionary<string, string>() {
		{ "RunInstances", "ImageId" },
		{ "DescribeInstances", "InstanceId.1" },
		{ "DescribeAccountAttributes", "AttributeName.1" },
		{ "GetUser", "" },
		{ "GetCallerIdentity", "" },
		{ "DescribeVolumes", "Filter.1.Value.1" },
		{ "DescribeTags", "Filter.1.Value.1" },
		{ "DescribeVpcs", "VpcId.1" },
		{ "DescribeInstanceAttribute", "InstanceId" },
		{ "DescribeInstanceCreditSpecifications", "InstanceId.1" },		
	};
	
	var resourceId = "TODO";

	if (resourceIdMap.ContainsKey(action))
	{
		var actionKey = resourceIdMap[action];
		if (String.IsNullOrEmpty(actionKey))
			resourceId = action;
		else
		{
			if (actionSegments.ContainsKey(actionKey))
				resourceId = actionSegments[actionKey];
			else
			{
				//Console.WriteLine("No key for " + actionKey);
			}
		}
	}
	else
	{
//		Console.WriteLine("No action key for " + action);
//		actionSegments.Dump("segments");
	}
	
	var request = new AwsRequest() { 
		Verb = "TODO",
		Action = action,
		RequestActions = actionSegments,
		RequestBody = String.Join("", requestBlock.Select(b => b.Message.Substring(b.Message.IndexOf(": "))).ToArray()),
		ResponseBody = String.Join("", contentBlock.Select(b => b.Message.Substring(b.Message.IndexOf(": "))).ToArray()),
		RequestTimestamp = requestBlock.First().Timestamp,
		ResponseTimestamp = headerBlock.First().Timestamp,		
		ResponseCode = "200",
		ResourceId = resourceId
	};

	if (action == "RunInstances")
	{
		// Swap in the real resource id
		if (request.ResponseBody.Contains("<instanceId"))
		{
			var instanceId = request.ResponseBody.Substring(request.ResponseBody.IndexOf("<instanceId>") + 12, 50).Split('<')[0];
			//Console.WriteLine($"Got instance id |{instanceId}|");
			request.ResourceId = instanceId;
		}
	}

	request.Elapsed = request.ResponseTimestamp.Subtract(request.RequestTimestamp);
	
	return request;

}



// Define other methods and classes here
public class TerraformRecord
{
	public int Index { get; set; }
	public DateTime Timestamp { get; set; }
	public string Level { get; set; }
	public string Message { get; set; }
}

public class AwsRequest
{
	public string Verb { get; set; }
	public string Action { get; set; }
	
	public string RequestBody { get; set; }
	public string ResponseBody { get; set; }

	public Dictionary<string, string> RequestActions { get; set; }
	
	public DateTime RequestTimestamp { get; set; }
	public DateTime ResponseTimestamp { get; set; }
	
	public string ResponseCode { get; set; }
	public TimeSpan Elapsed { get; set; }
	public string ResourceId { get; set; }
	
	public AwsRequest[] SubRequests { get; set; }
}
 
public class Trace
{
	public int id { get; set; }
	public string name { get; set; }
	public string resultType { get; set; }
	public bool hidden { get; set; }
	public bool systemHidden { get; set; } 
	public long startNanos { get; set; }
	public long endNanos { get; set; }  
}

public class Relationship { 
	public string relationship { get; set; }
	public int from { get; set; }
	public int to { get; set; } 
}

public class Graph { 
	public Trace[] traces { get; set; }
	public Relationship[] relationships { get; set; }
}