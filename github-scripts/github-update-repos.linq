<Query Kind="Program">
  <NuGetReference>Octokit</NuGetReference>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Octokit</Namespace>
</Query>

void Main()
{
	var token = File.ReadAllText(@"C:\code\workspace\github-scripts\personal-token.txt");
	DoStuffAsync(token).GetAwaiter().GetResult();
}

private static readonly string productHeaderValue = "azurecat-testapi";

async Task DoStuffAsync(string token) 
{ 
	// http://octokitnet.readthedocs.io/en/latest/getting-started/
	var client = new GitHubClient(
		new Octokit.ProductHeaderValue(productHeaderValue));
	var tokenAuth = new Credentials(token);
	client.Credentials = tokenAuth;

	// Dump all repos
	var repos = await client.Repository.GetAllForOrg("az-cat");
	repos.Select(r => new
	{
		r.Name,
		r.FullName,
		r.GitUrl,
		r.CreatedAt, 
		r.Fork,
		r.Private,
		r.UpdatedAt
	}).Dump("repos");
	
	var r = repos.First();
	
	client.
	
	
}
