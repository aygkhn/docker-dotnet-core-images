#addin nuget:?package=Cake.Json
#addin nuget:?package=Cake.Docker
#addin nuget:?package=Cake.Http
#addin nuget:?package=Newtonsoft.Json&version=9.0.1
#addin nuget:?package=System.Net.Http&version=4.3.3

using Cake.Json;
using Cake.Docker;
using Newtonsoft.Json;
using System.Net.Http;

public class Manifest
{
    public Repo[] repos { get; set; }
    public Test[] tests { get; set; }
}

public class Repo
{
    public string name { get; set; }
    public string readmePath { get; set; }
    public Image[] images { get; set; }
}

public class Image
{
    public int id { get; set; }
    public string name { get; set; }
    public string osType { get; set; }
    public string os { get; set; }
    public string dockerfile { get; set; }
    public string[] tags { get; set; }
}

public class Test
{
    public int id { get; set; }
    public string name { get; set; }
    public string testAppPath { get; set; }
    public string[] buildArgs { get; set; }
    public int port { get; set; }
    public string[] httpCalls { get; set; }
}

var manifest = DeserializeJsonFromFile<Manifest>("manifest.json");
var target = Argument("target", "Default");

Task("Default")
    .IsDependentOn("Tests");

Task("Build-Containers")
  .Does(() =>
{
  Information("Hello World!");  
  IList<string> tags = new List<string>();

  foreach(Repo repo in manifest.repos)
  {
    foreach(Image img in repo.images)
    {
      Information("Building " + repo.name + img.tags[0]);

      DockerImageBuildSettings settings = new DockerImageBuildSettings();
      settings.File = img.dockerfile + "Dockerfile";
      settings.Tag = new string[img.tags.Length];

      for (int i = 0; i < img.tags.Length; i++)
      {
          settings.Tag[i] = repo.name + ":" + img.tags[i];
          tags.Add(settings.Tag[i]);
      }

      DockerBuild(settings, img.dockerfile);

      Information("Build complete " + repo.name + img.tags[0]);
    }
  }
});

Task("Tests")
  .IsDependentOn("Build-Containers")
  .Does(() =>{
    foreach (var test in manifest.tests)
    {
      DockerImageBuildSettings settings = new DockerImageBuildSettings();
      settings.File = test.testAppPath + "Dockerfile";
      settings.Tag = new [] {test.name};
      settings.BuildArg = test.buildArgs;
      DockerBuild(settings, test.testAppPath);

      Information("Build complete " + test.name);
      if(test.httpCalls != null)
      {
        Information("Running " + test.name);
        DockerContainerRunSettings runSettings = new DockerContainerRunSettings();
        runSettings.Detach = true;
        runSettings.Publish = new [] {test.port + ":80"};

        DockerRun(runSettings, test.name, null);

        System.Threading.Thread.Sleep(3000);

        HttpClient client = new HttpClient();

        foreach (var httpCall in test.httpCalls)
        {
            string url = string.Format("http://localhost:{0}{1}", test.port, httpCall);

            Information("Http call " + url);
            try
            {
              HttpResponseMessage response = client.GetAsync(url).GetAwaiter().GetResult();

              if (response.IsSuccessStatusCode)
              {
                Information("Http call Success " + url);
              }
              else
              {
                throw new Exception("Not Success " + url);
              }
            }
            catch (System.Exception ex)
            {
              Error(ex);
              throw ex;
            }
        }       
      }
    }
  });

Task("Publish")
    .Does(() =>
{
  IList<string> tags = new List<string>();

  foreach(Repo repo in manifest.repos)
  {
    foreach(Image img in repo.images)
    {
      for (int i = 0; i < img.tags.Length; i++)
      {
          string pushTag = repo.name + ":" + img.tags[i];
          
          DockerPush(pushTag);
      }
    }
  }
});

RunTarget(target);