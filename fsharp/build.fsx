// include Fake libs
#r "./packages/build/FAKE/tools/FakeLib.dll"

open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open System
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

Target "Build" (fun _ ->
    // compile all projects below src/app/
    MSBuildDebug buildDir "Build" appReferences
    |> Log "AppBuild-Output: "
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
  ==> "Build"
  ==> "Test"
  ==> "Deploy"

// start build
RunTargetOrDefault "Build"
