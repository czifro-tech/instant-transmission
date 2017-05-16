namespace MUDT.Test

  open System
  open System.IO
  open MUDT.IO
  open MUDT.Diagnostics
  open MUDT.Cryptography
  open MUDT.Net
  open MUDT.Net.Protocol
  open MUDT.Net.Sockets
  open MUDT.IO.MMFPartition

  module FTPUnitTest =

    let wait() =
      200 |> Async.Sleep |> Async.RunSynchronously
    
    let createConfig (partitionCount:int) (file':FileInfo) =
      let bufferCapacity' = ((file'.Length / int64(partitionCount)) / 100L)
      let hashBlockSize = int ((file'.Length / int64(partitionCount) / 8L))
      printfn "Hash Block Size: %d, BufferCapacity: %d, FileLength: %d" hashBlockSize bufferCapacity' file'.Length
      {
        hashStateConfig = HashStateConfig.BackloggingMd5Config(hashBlockSize)
        directory = file'.Directory.FullName;
        file = file';
        partitionCount = partitionCount
        bufferCapacity = bufferCapacity'
      }

    let private mmfSetup isClient =
      //let fileName = "TeamContactInfo.txt"
      let fileName = "Der Doppelganger copy.mp4"
      //let fileName = "rockyou.txt"
      //let fileName = "32_new.txt"
      let if' =  "/Users/czifro/Dropbox/" + fileName
      printf "Enter full path of input file: %s\n" if'
      //let if' = Console.ReadLine()
      let of' =  "/Users/czifro/.mudt/" + fileName
      printf "Enter full path of output file: %s\n" of'
      //let of' = Console.ReadLine()
      Helper.use4GBMemoryLimit()
      let partitionCount = 8
      let config =
        if not <| isClient then
          (MemoryMappedFile.tryGetFileInfo(if')).Value
          |> createConfig partitionCount
        else
          (MemoryMappedFile.createFileAsync (of') (MemoryMappedFile.tryGetFileInfo(if')).Value.Length)
          |> Async.RunSynchronously |> ignore
          (MemoryMappedFile.tryGetFileInfo(of')).Value
          |> createConfig partitionCount

      (config, partitionCount)

    let port = 50125

    let ``Server Local Transmission Test with No Retransmit``() =
      printfn "Running ``Server Local Transmission Test with No Retransmit``..."
      let config,partitionCount = mmfSetup false
      let serverState = FTP.configureFTPState config FTPType.Sender partitionCount
      try
      try
      let serverState = FTP.waitForConnectionAsync serverState port |> Async.RunSynchronously
      let serverStateOption = FTP.handshakeAsync serverState [||] |> Async.RunSynchronously
      if serverStateOption.IsNone then
        failwith "Handshake failed..."
      let serverState = serverStateOption.Value
      let st,_ = Helper.timeMemTickTock()
      let sendFileRetOption = FTP.sendFileAsync serverState false |> Async.RunSynchronously
      if sendFileRetOption.IsNone then
        failwith "Transmission failed..."
      let et,_ = Helper.timeMemTickTock()
      printfn "Transmission Duration: %s" ((et - st).ToString())
      with
      | ex ->
        failwithf "Something happened: %s" (ex.ToString())
      ()
      finally
        serverState.server.Socket.Dispose()
        serverState.client.Socket.Dispose()

    let ``Client Local Transmission Test with No Retransmit``() =
      printfn "Running ``Client Local Transmission Test with No Retransmit``..."
      let config,partitionCount = mmfSetup true
      let clientState = FTP.configureFTPState config FTPType.Receiver partitionCount
      try
      try
      let clientState = FTP.connectAsync clientState "127.0.0.1" port |> Async.RunSynchronously
      let clientStateOption = 
        FTP.handshakeAsync clientState (PortChecker.getAvailablePorts()) |> Async.RunSynchronously
      if clientStateOption.IsNone then
        failwith "Handshake failed..."
      let clientState = clientStateOption.Value
      let receiveFileRetOption = FTP.receiveFileAsync clientState false |> Async.RunSynchronously
      if receiveFileRetOption.IsNone then
        failwith "Transmission failed..."
      with
      | ex ->
        failwithf "Something happened: %s" (ex.ToString())
      ()
      finally
        clientState.server.Socket.Dispose()

    let private tests() =
      [|
        ``Server Local Transmission Test with No Retransmit``;
        ``Client Local Transmission Test with No Retransmit``;
      |]

    let testRunner op =
      Helper.testRunner tests () op
