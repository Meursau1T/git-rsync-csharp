using System.CommandLine;
using System.Diagnostics;

List<string> runCmd(string cmd, string arg) {
    var proc = new Process {
        StartInfo = new ProcessStartInfo
        {
            FileName = cmd,
            Arguments = arg,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        }
    };
    proc.Start();
    List<string> res = new List<string>();
    while (!proc.StandardOutput.EndOfStream) {
      var line = proc.StandardOutput.ReadLine();
      if (line != null && line.Length > 0) {
        res.Add(line);
      }
    }
    return res;
}

List<string> asyncByLog(List<string> currEdit) {
    var logPath = "/tmp/rsync-git";
    if (!File.Exists(logPath)) {
      Console.WriteLine("File doesn't exist. Created a new one.");
      var fs = File.Create(logPath);
      fs.Close();
    }
    var lastEdit = File.ReadLines(logPath).ToList();
    Console.WriteLine("Curr edit files:");
    currEdit.ForEach(item => Console.WriteLine(item));
    Console.WriteLine("Last edit files:");
    lastEdit.ForEach(item => Console.WriteLine(item));
    File.WriteAllLines(logPath, currEdit);
    List<string> bothEdit = new List<string>();
    bothEdit.AddRange(currEdit);
    bothEdit.AddRange(lastEdit);
    return bothEdit.Distinct().ToList();
}

List<string> getGitEdit() {
    var rawRes = runCmd("git", "status --porcelain");
    var res = rawRes.Select(item => item.Split(" ").Last()).Where(item => item.Length > 0).ToList();
    return res;
}

string getDir(string path) {
  var lastIdx = path.LastIndexOf("/");
  if (lastIdx > 0) {
    return path.Substring(0, lastIdx);
  } else {
    return path;
  }
}

void callRsync(string path, string localPath, string remotePath, string userIp, string rsyncParam, bool showLog) {
  var origPath = $"{localPath}/{path}";
  var tgtPath = getDir($"{userIp}:{remotePath}/{path}");
  var cmd = $"-av{rsyncParam}";
  Console.WriteLine($"cmd: rsync {cmd} {origPath} {tgtPath}");
  var runRes = runCmd("rsync", $"{cmd} {origPath} {tgtPath}");
  if (rsyncParam.Contains("n") || showLog) {
    Console.WriteLine("Rsync result:");
    runRes.ForEach(item => Console.WriteLine(item));
  }
}

var rootCommand = new RootCommand("Rsync git modified folder to remote");
var localPath = new Option<string>(name: "-l", description: "Local path of target folder") { IsRequired = true };
var remotePath = new Option<string>(name: "-r", description: "Remote path of target folder") { IsRequired = true };
var userIp = new Option<string>(name: "-u", description: "Remote server ip address") { IsRequired = true };
var rsyncParam = new Option<string>(name: "-p", description: "Additional params of rsync", getDefaultValue: () => "");
var disableGit = new Option<bool>(name: "-d", description: "Disable sync .git folder", getDefaultValue: () => true);
var showLog = new Option<bool>(name: "-v", description: "Show log", getDefaultValue: () => true);

rootCommand.AddOption(localPath);
rootCommand.AddOption(remotePath);
rootCommand.AddOption(userIp);
rootCommand.AddOption(rsyncParam);
rootCommand.AddOption(disableGit);
rootCommand.AddOption(showLog);

rootCommand.SetHandler((localPath, remotePath, userIp, rsyncParam, disableGit, showLog) => {
  var currEditFiles = asyncByLog(getGitEdit());
  var currEditDirs = currEditFiles.Distinct().ToList();
  if (showLog) {
    Console.WriteLine("Rsync final dir list:");
    currEditDirs.ForEach(item => Console.WriteLine(item));
  }
  currEditDirs.ForEach(item => callRsync(item, localPath, remotePath, userIp, rsyncParam, showLog));
  Console.WriteLine($"disableGit:{disableGit}");
  if (!disableGit) {
    callRsync(".git", localPath, remotePath, userIp, rsyncParam, showLog);
  }
}, localPath, remotePath, userIp, rsyncParam, disableGit, showLog);

return await rootCommand.InvokeAsync(args);
