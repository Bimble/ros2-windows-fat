#addin Cake.Curl
#addin "Cake.Powershell"
#addin "Cake.FileHelpers"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var unpackedPythonDirectory = "python-3.7.0.amd64";

// Define directories.
var buildDir = Directory("./output");
var downloadDir = Directory("./downloads");
var ros2Dir = buildDir + Directory("ros2-windows");
var dependenciesDir = ros2Dir + Directory("Dependencies");
var winPythonDir = dependenciesDir + Directory("WinPython");
var sslDir = dependenciesDir + Directory("ssl");

// Define files to download.
var pythonFile = new FilePath(downloadDir + new FilePath("WinPython.exe"));
var cmakeFile = new FilePath(downloadDir + new FilePath("CMake.zip"));
var asioFile = new FilePath(downloadDir + new FilePath("asio.nupkg"));
var eigenFile = new FilePath(downloadDir + new FilePath("eigen.nupkg"));
var tinyxml2File = new FilePath(downloadDir + new FilePath("tinyxml2.nupkg"));
var tinyxml_usestlFile = new FilePath(downloadDir + new FilePath("tinyxml-usestl.nupkg"));
var ssl = new FilePath(downloadDir + new FilePath("ssl.exe"));
var ros = new FilePath(downloadDir + new FilePath("ros.zip"));
var files = new List<(FilePath FilePath, Uri Uri)>()
{
    (pythonFile, new Uri("https://github.com/winpython/winpython/releases/download/1.10.20180827/WinPython64-3.7.0.2Zero.exe")),
    (cmakeFile, new Uri("https://cmake.org/files/v3.13/cmake-3.13.0-rc1-win64-x64.zip")),
    (asioFile, new Uri("https://github.com/ros2/choco-packages/releases/download/2018-06-12-1/asio.1.12.1.nupkg")),
    (eigenFile, new Uri("https://github.com/ros2/choco-packages/releases/download/2018-06-12-1/eigen.3.3.4.nupkg")),
    (tinyxml_usestlFile, new Uri("https://github.com/ros2/choco-packages/releases/download/2018-06-12-1/tinyxml-usestl.2.6.2.nupkg")),
    (tinyxml2File, new Uri("https://github.com/ros2/choco-packages/releases/download/2018-06-12-1/tinyxml2.6.0.0.nupkg")),
    (ssl, new Uri("https://slproweb.com/download/Win64OpenSSL-1_0_2p.exe")),
    (ros, new Uri("https://github.com/ros2/ros2/releases/download/release-bouncy-20180824/ros2-bouncy-windows-AMD64.zip"))
};


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(buildDir);
    CleanDirectory(ros2Dir);
    CleanDirectory(dependenciesDir);
    CleanDirectory(downloadDir);
});

Task("Download")
    //.IsDependentOn("Clean")
    .Does(() =>
{
    CurlDownloadFiles(
        files.Select(f => f.Uri),
        new CurlDownloadSettings
        {
            FollowRedirects = true,
            OutputPaths = files.Select(f => f.FilePath)
        });
});

Task("Setup Python")
    .IsDependentOn("Download")
    .Does(() =>
{
    //NSIS installer, only works with backslash...
    var absFolder = MakeAbsolute(winPythonDir).FullPath.Replace('/', '\\');
    StartProcess(MakeAbsolute(pythonFile) , new ProcessSettings{Arguments = "/S /D=" + absFolder});

    //Make winpython movable
    StartProcess(MakeAbsolute(new FilePath(winPythonDir + new FilePath("scripts/make_winpython_movable.bat"))), new ProcessSettings{Arguments = "<nul"});
    //pip install additional packages
    StartProcess(new FilePath(winPythonDir + new FilePath(unpackedPythonDirectory +"/python.exe")), new ProcessSettings{Arguments = "-m pip install -U catkin_pkg empy pyparsing pyyaml setuptools"});
});

Task("Setup CMake")
    .IsDependentOn("Download")
    .Does(() =>
{
    Unzip(cmakeFile, dependenciesDir);
});

Task("Setup Nuget Libaries")
    .IsDependentOn("Download")
    .Does(() =>
{
    var packages = new[]{"asio", "eigen", "tinyxml2", "tinyxml-usestl"};
    foreach (var package in packages)
    {
        StartPowershellScript("Install-Package " + package + " -Source " + MakeAbsolute(downloadDir).FullPath + " -Destination " + MakeAbsolute(dependenciesDir));
    }
});

Task("Setup SSL")
    .IsDependentOn("Download")
    .Does(() =>
{
    StartProcess(MakeAbsolute(ssl) , new ProcessSettings{Arguments = "/verysilent /dir=" + sslDir});
});

Task("Setup ROS")
    .IsDependentOn("Download")
    .Does(() =>
{
    Unzip(ros, buildDir);

    //Patch
    ReplaceTextInFiles(buildDir + new FilePath("ros2-windows/local_setup.bat"), "c:\\python37\\python.exe", "%~dp0Dependencies\\WinPython\\"+ unpackedPythonDirectory +"\\python.exe");
});

Task("Pack")
    .IsDependentOn("Setup Python")
    .IsDependentOn("Setup CMake")
    .IsDependentOn("Setup Nuget Libaries")
    .IsDependentOn("Setup SSL")
    .IsDependentOn("Setup ROS")
    .Does(() =>
{
    Zip(buildDir, buildDir + new FilePath("ros.zip"));
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);