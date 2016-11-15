// include Fake libs
#r "./packages/build/FAKE/tools/FakeLib.dll"

open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open System
open System.Collections.Generic
open System.Text
open System.IO
open System.IO.Compression
open System.Diagnostics
open System.Security.AccessControl
open Fake.Testing.NUnit3

let project = "MUDT"

let summary = "A light weight protocol for concurrent data transferring"

let authors = [ "Will Czifro" ]

let testAssemblies = "build/*Test*.dll"

let gitOwner = "czifro-tech"

let gitHome = "https://github.com/" + gitOwner

let gitName = "mudt"

let gitRaw = environVarOrDefault "gitRaw" "https://raw.github.com/czifro-tech"

// Directories
let buildDir  = "./build/"
let deployDir = "./deploy/" 

let dotnetcliVersion = "1.0.0-preview3-004056"

let dotnetCliPath = DirectoryInfo "./dotnetcore"

let netcoreFiles = !! "src/**/*.fsproj" |> Seq.toList

// Filesets
let appReferences  =
    !! "**/**/*.fsproj"

// version info
let version = "0.1"  // or retrieve from CI server

let genFSAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let folderName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(projectPath))
    let basePath = "src" @@ folderName
    let fileName = basePath @@ "AssemblyInfo.fs"
    CreateFSharpAssemblyInfo fileName
      [ Attribute.Title (projectName)
        Attribute.Product project
        Attribute.Company (authors |> String.concat ", ")
        Attribute.Description summary
        Attribute.Version version
        Attribute.FileVersion version
        Attribute.InformationalVersion version ]

let getConfigText (config:FileInfo) =
    use s = config.OpenText()
    s.ReadToEnd()

let createConfigWithText (fullName:string) (text:string) =
    let config = FileInfo(fullName)
    use s = config.CreateText()
    s.Write(text)
    config

// Targets

Target "AssemblyInfo" (fun _ ->
   !! "src/**/*.fsproj"
   |> Seq.iter genFSAssemblyInfo
)

// Target "InstallDotNetCore" (fun _ ->
//     let dotnet = if isWindows then "dotnet.exe" else "dotnet"
//     let correctVersionInstalled =
//         try
//             let processResult =
//                 ExecProcessAndReturnMessages (fun info ->
//                     info.FileName <- dotnet
//                     info.WorkingDirectory <- Environment.CurrentDirectory
//                     info.Arguments <- "-version"
//                 ) (TimeSpan.FromMinutes 30.)
//             processResult.Messages |> separated "" = dotnetcliVersion
//         with
//         | _ -> false
//     if correctVersionInstalled then
//       tracefn "dotnetcli %s already installed" dotnetcliVersion
//     else
//       let url =
//         if isWindows then
//           "https://download.microsoft.com/download/0/A/3/0A372822-205D-4A86-BFA7-084D2CBE9EDF/DotNetCore.1.0.1-SDK.1.0.0.Preview2-003133-x64.exe"
// )

Target "Clean" (fun _ ->
    CleanDirs [buildDir; deployDir]
)

Target "CopyNativeDependencies" (fun _ ->
    let nativeDir = 
        let rootPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)
        sprintf "%s%s%s" rootPath directorySeparator ".native"
    if Directory.Exists(nativeDir) then
        tracefn "%s already exists" nativeDir
    else
        let userDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)
        let localNativeDirOpt =
            let mutable curDir = DirectoryInfo(currentDirectory)
            let search (dirs:DirectoryInfo[]) =
                dirs |> Array.tryFind(fun x -> 
                    x.FullName.EndsWith(".native")
                )
            while (search (curDir.GetDirectories())).IsNone do
                curDir <- curDir.Parent
            (search (curDir.GetDirectories()))
        if localNativeDirOpt.IsNone then failwith "Could not find .native"
        let localNativeDir = localNativeDirOpt.Value.FullName
        Directory.CreateDirectory(nativeDir) |> ignore
        tracefn "Copying native libs from %s to %s" localNativeDir nativeDir
        let files = (DirectoryInfo(localNativeDir)).GetFiles()
        for file in files do
            file.CopyTo(Path.Combine(nativeDir, file.Name), true) |> ignore
        
        tracefn "Successfully copied native libs"
)

Target "Build" (fun _ ->
    // compile all projects below src/app/
    let genFiles = MSBuildDebug buildDir "Build" appReferences
    genFiles |> Log "AppBuild-Output: "
    tracefn "%A" genFiles
    tracefn "Copying MUDT.config to MUDT.dll.config in build directory"
    let mudtDll = genFiles |> List.find(fun x -> x.EndsWith(project + ".dll"))
    let mudtTestDll = genFiles |> List.find(fun x -> x.EndsWith(project + ".Test.dll"))
    let mudtDllConfig = mudtDll + ".config"
    let appConfigPath = Path.Combine(currentDirectory, "MUDT.config")
    let appConfig = FileInfo(appConfigPath)
    let appConfigText = getConfigText appConfig
    let mudtDllConfigText = appConfigText.Replace("{dir}", "/Users/czifro/bin/Linux.x64.Debug/Native")//Path.Combine(appConfig.Directory.FullName, ".native"))
    let config = createConfigWithText mudtDllConfig mudtDllConfigText
    ignore <| config.CopyTo(Path.Combine(config.Directory.FullName, mudtTestDll + ".config"))
)

Target "Test" (fun _ ->
    !! "src\MUDT\MUDT.fsproj"
    |> MSBuildDebug "" "Build"
    |> ignore

    !! testAssemblies
    |> NUnit3 (fun p ->
        {
            p with
                ShadowCopy = false
                WorkingDir = "test/MUDT.Test"
                TimeOut = TimeSpan.FromMinutes 20.
        })
)

Target "Deploy" (fun _ ->
    !! (buildDir + "/**/*.*")
    -- "*.zip"
    |> Zip buildDir (deployDir + "ApplicationName." + version + ".zip")
)

// Build order
"Clean"
  ==> "CopyNativeDependencies"
  ==> "Build"
  ==> "Test"
  ==> "Deploy"

// start build
RunTargetOrDefault "Build"
