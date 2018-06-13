<Query Kind="Program">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
</Query>

void Main()
{
	var file = @"C:\Users\masimms\Documents\terraform-log-create-vm2.txt";
	var index = 0;
	var contents = File.ReadAllLines(file)		
		.Where(f => f.StartsWith("2018"))		
		.Select(f => new TerraformRecord
		{
			Index = index++,
            Timestamp = DateTime.Parse(f.Substring(0, f.IndexOf('[')-1)),
			Level = f.Substring(f.IndexOf('[')+1, f.IndexOf(']')-f.IndexOf('[')-1),
			Message = f.Substring(f.IndexOf(']')+2)
		})
		.ToArray()
	;
	
 
	var apiCalls = contents.Where(
		c => c.Message.Contains("GET") 
	 || c.Message.Contains("PUT") 
	 || c.Message.Contains("POST") 
	 || c.Message.Contains("PATCH") 
	 || c.Message.Contains("DELETE") 
	)
		.Where(c => !c.Message.Contains("/operations/"))
		.ToArray()
	;

	var operationCalls = contents
		.Where(c => c.Message.Contains("GET"))		
		.Where(c => c.Message.Contains("/operations/"));
	//operationCalls.Dump("async operations");

	var apis = GetArmRequests(contents, apiCalls);
	
//	
//	var apis = apiCalls.Select(c => new
//	{
//        Request = c,
//		Response = FindMatchingResponse(contents, c),
//	})
//	.Select(c => new ArmRequest
//	{
//		Verb = GetVerb(c.Request.Message),
//		Elapsed = c.Response.Timestamp.Subtract(c.Request.Timestamp),
//		ResponseCode = contents[c.Response.Index+1].Message.Substring(contents[c.Response.Index+1].Message.IndexOf(':')+2),
//		Request = c.Request,
//		Response = c.Response,
//		ResourceId = c.Request.Message.Substring(
//			c.Request.Message.IndexOf("/subscriptions")).Replace(" HTTP/1.1", "")
//	})	
	//.Dump()
	;
	
	// Look for async (201) calls, then find their matching GET requests and collapse
	var asyncApis = apis
		.Where(a => a.ResponseCode.Contains("201"))
		.Select(a => new
		{
		    request = a,
			asyncId = FindAsyncOperationId(contents, a)		
		});
		
	//asyncApis.Dump("async");
	
	var operationsApis = apis.Where(a => a.Request.Message.Contains("operations"));
	//operationsApis.Dump("operations");

	int x = 1;
	
	var traces = apis.Select(a => new Trace
	{
		id = x++,
		name = GetResource(a.ResourceId),
		resultType = "SUCCESS",
		hidden = false,
		systemHidden = false,
		startNanos = a.Request.Timestamp.Ticks * 100,
		endNanos = a.Response.Timestamp.Ticks * 100
	})
	.ToArray()
	;
	
	
	var relations = traces.Select(t => new Relationship
	{
        relationship = "PARENT_OF",
		from = 0,
		to = t.id
	})
	.ToArray()
	;
	
	var graph = new Graph { 
		 traces = traces,
		 relationships = relations
	};

//	graph.traces.Dump("test");
//	Console.WriteLine(
//		JsonConvert.SerializeObject(graph));
//		

	//apiCalls.Dump();
}

public ArmRequest[] GetArmRequests(TerraformRecord[] contents, TerraformRecord[] apiCalls)
{
	var apis = apiCalls.Select(c => new
	{
		Request = c,
		Response = FindMatchingResponse(contents, c),
	})
	.Select(c => new ArmRequest
	{
		Verb = GetVerb(c.Request.Message),
		Elapsed = c.Response.Timestamp.Subtract(c.Request.Timestamp),
		ResponseCode = contents[c.Response.Index + 1].Message.Substring(contents[c.Response.Index + 1].Message.IndexOf(':') + 2),
		Request = c.Request,
		Response = c.Response,
		ResourceId = c.Request.Message.Substring(
			c.Request.Message.IndexOf("/subscriptions")).Replace(" HTTP/1.1", "")
	})
	.ToArray();
	return apis;
}

public string GetVerb(string message)
{
	var verb = message.Substring(message.IndexOf(':')+2, 16);
	var verb2 = verb.Substring(0, verb.IndexOf('/'));
	return verb2;
}

public string GetResource(string armId)
{	
	String ret;
	if (armId.Contains("providers"))
		ret = armId.Substring( armId.IndexOf("providers"));
	else if (armId.Contains("resourcegroups"))
		ret = armId.Substring( armId.IndexOf("resourcegroups"));
	else 
		ret = armId;	
	
	ret = ret
		.Substring(0, ret.IndexOf('?'))
		.Replace("providers/", "");
	//Console.WriteLine(ret);
	return ret;
}

public string FindAsyncOperationId(
	TerraformRecord[] logs, ArmRequest asyncRequest)
{
	var asyncOperation = logs
		.Where(l => l.Index > asyncRequest.Request.Index)
		.Where(l => l.Message.Contains("Azure-Asyncoperation" ))
		.FirstOrDefault();		
	if (asyncOperation == null)
		return String.Empty;
	var response = asyncOperation.Message.Substring(
		asyncOperation.Message.IndexOf("Azure-Asyncoperation:") + 22);
			
	return response;
}

public TerraformRecord FindMatchingAsyncResponse(
	TerraformRecord[] logs, ArmRequest asyncRequest)
{
	// Find the next operation GET request for that resource (TODO - successful "done" request :)
	var operations = logs
		.Where(a => a.Message.Contains("operations"));
	
	//var asyncCheck = 
	return null;	
}

public TerraformRecord FindMatchingResponse( 
	TerraformRecord[] logs, TerraformRecord request)
{
	// Get the resource identifier from the request
	var resourceId = request.Message.Substring(
			request.Message.IndexOf("/subscriptions"))
		.Replace(" HTTP/1.1", "")		
		;
		
	return logs
		.Where(l => l.Index > request.Index )
		.Where(l => l.Message.Contains("AzureRM Response"))
		.Where(l => l.Message.Contains(resourceId))
		.FirstOrDefault()
		;
}

// Define other methods and classes here
public class TerraformRecord
{
	public int Index { get; set; }
	public DateTime Timestamp { get; set; }
	public string Level { get; set; }
	public string Message { get; set; }
}

public class ArmRequest
{
	public string Verb { get; set; }
	public TerraformRecord Request { get; set; }
	public TerraformRecord Response { get; set; } 
	public string ResponseCode { get; set; }
	public TimeSpan Elapsed { get; set; }
	public string ResourceId { get; set; }
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