namespace MUDT.IO

  open System
  open System.IO
  open MUDT.IO
  open MUDT.Cryptography
  open MUDT.Diagnostics
  open MUDT.Utilities
  
  // type MemoryMappedFileStateConfig =
  //   {
  //     hashStateConfig : HashStateConfig;
  //     directory : string
  //   }

  //   static member private DefaultInstance () =
  //     {
  //       hashStateConfig = HashStateConfig.DefaultInstance();
  //       directory = Environment.CurrentDirectory
  //     }

  //   static member 

  type MemoryMappedFileState = 
    {
      partitions : MMFPartitionState[];
      fileInfo : FileInfo;
    }

  module MemoryMappedFile =

    let tryGetFileInfo (fileName:string) =
      let info = FileInfo(fileName)
      if info.Exists then
       Some info
      else
       None

    let private padFileAsync (info:FileInfo) (len:int64) =
      async {
        use fs = info.Create()
        let segSize = 
          int([|
                len / 10L; // this is the suggested size
                int64(CurrentProcessInfo.getMemoryLimit() / 10); // we cannot use too much memory
                int64(Int32.MaxValue / 10); // this is the cap for how big we can go for performance reasons
              |] |> Array.min)

        
        while fs.Length < len do
          let size = if (fs.Length + int64(segSize)) > len then int(len - fs.Length) else segSize
          do! size |> TypeUtility.nullByteArray |> fs.AsyncWrite
      }

    let createFileAsync (fileName:string) (fileLength:int64) =
      async {
        let info = FileInfo(fileName)
        if not <| info.Exists then
          do! padFileAsync info fileLength
          
      }
