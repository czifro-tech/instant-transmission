namespace MUDT.Cryptography

  open MUDT.Utilities.TypeUtility

  type MD5State = 
    {
      state : int[];
      count : int[];
      buffer : byte[];
    }

    static member DefaultInstance =
      {
        state = 
          [| 0x67452301; 0xefcdab89; 0x98badcfe; 0x10325476; |]
        count = [| 0; 0; |]
        buffer = nullByteArray 64
      }

  /// <summary>
  /// The following code, as well as above, comes from http://motp.sourceforge.net/MD5.java
  /// The difference between the two is that the MD5State is not
  /// cached by this module. This module simply performs computations on that state
  /// </summary>
  module MD5 =

    let private stateToTuple (state:int[]) =
      (state.[0], state.[1], state.[2], state.[3])
    let private padding =
      Array.append [| byte(0x80) |] (nullByteArray 63)

    let private rotateLeft x n =
      (x <<< n) ||| ( x >>> (32 - n))

    let private uadd a b =
      let aa = int64(a) &&& 0xFFFFFFFFL
      let bb = int64(b) &&& 0xFFFFFFFFL
      int((aa+bb)&&&0xFFFFFFFFL)

    let private uadd3 a b c =
      uadd (uadd a b) c

    let private uadd4 a b c d =
      uadd (uadd3 a b c) d

    let private step transformStep (a:int) (b:int) (c:int) (d:int) (x:int) (s:int) (ac:int) =
      let res = transformStep a b c d x s ac
      uadd (rotateLeft res s) b

    let private FF (a:int) (b:int) (c:int) (d:int) (x:int) (s:int) (ac:int) =
      uadd4 a ((b &&& c) ||| (~~~b &&& d)) x ac
      
    let private GG (a:int) (b:int) (c:int) (d:int) (x:int) (s:int) (ac:int) =
      uadd4 a ((b &&& d) ||| (c &&& ~~~d)) x ac

    let private HH (a:int) (b:int) (c:int) (d:int) (x:int) (s:int) (ac:int) =
      uadd4 a (b ^^^ c ^^^ d) x ac

    let private II (a:int) (b:int) (c:int) (d:int) (x:int) (s:int) (ac:int) =
      uadd4 a (c ^^^ (b ||| ~~~d)) x ac

    let private decode (buffer:byte[]) (len:int) (shift:int) =
      let out = [| for i in 0..16 -> 0 |]
      let mutable (i,j) = (0,0)
      let ff = 0xFF |> byte
      out |> Array.iteri(fun i x -> 
        if i % 4 = 0 then
          out.[i] <- (int(buffer.[i + shift] &&& ff)) |||
                     (int(buffer.[i + 1 + shift] &&& ff) <<< 8) |||
                     (int(buffer.[i + 2 + shift] &&& ff) <<< 16) |||
                     (int(buffer.[i + 3 + shift] &&& ff) <<< 24)
        else
          ignore 0
      )
      out

    let private ffTransforms (tup:int*int*int*int*int[]) =
      let mutable (a,b,c,d,x) = tup
      a <- step FF a b c d x.[0] 7 0xd76aa478
      d <- step FF d a b c x.[1] 12 0xe8c7b756
      c <- step FF c d a b x.[2] 17 0x242070db
      b <- step FF b c d a x.[3] 22 0xc1bdceee
      a <- step FF a b c d x.[4] 7 0xf57c0faf
      d <- step FF d a b c x.[5] 12 0x4787c62a
      c <- step FF c d a b x.[6] 17 0xa8304613
      b <- step FF b c d a x.[7] 22 0xfd469501
      a <- step FF a b c d x.[8] 7 0x698098d8
      d <- step FF d a b c x.[9] 12 0x8b44f7af
      c <- step FF c d a b x.[10] 17 0xffff5bb1
      b <- step FF b c d a x.[11] 22 0x895cd7be
      a <- step FF a b c d x.[12] 7 0x6b901122
      d <- step FF d a b c x.[13] 12 0xfd987193
      c <- step FF c d a b x.[14] 17 0xa679438e
      b <- step FF b c d a x.[15] 22 0x49b40821
      (a,b,c,d,x)

    let private ggTransforms (tup:int*int*int*int*int[]) =
      let mutable (a,b,c,d,x) = tup
      a <- step GG a b c d x.[1] 5 0xf61e2562
      d <- step GG d a b c x.[6] 9 0xc040b340
      c <- step GG c d a b x.[11] 14 0x265e5a51
      b <- step GG b c d a x.[0] 20 0xe9b6c7aa
      a <- step GG a b c d x.[5] 5 0xd62f105d
      d <- step GG d a b c x.[10] 9 0x2441453
      c <- step GG c d a b x.[15] 14 0xd8a1e681
      b <- step GG b c d a x.[4] 20 0xe7d3fbc8
      a <- step GG a b c d x.[9] 5 0x21e1cde6
      d <- step GG d a b c x.[14] 9 0xc33707d6
      c <- step GG c d a b x.[3] 14 0xf4d50d87
      b <- step GG b c d a x.[8] 20 0x455a14ed
      a <- step GG a b c d x.[13] 5 0xa9e3e905
      d <- step GG d a b c x.[2] 9 0xfcefa3f8
      c <- step GG c d a b x.[7] 14 0x676f02d9
      b <- step GG b c d a x.[12] 20 0x8d2a4c8a
      (a,b,c,d,x)

    let private hhTransforms (tup:int*int*int*int*int[]) =
      let mutable (a,b,c,d,x) = tup
      a <- step HH a b c d x.[5] 4 0xfffa3942
      d <- step HH d a b c x.[8] 11 0x8771f681
      c <- step HH c d a b x.[11] 16 0x6d9d6122
      b <- step HH b c d a x.[14] 23 0xfde5380c
      a <- step HH a b c d x.[1] 4 0xa4beea44
      d <- step HH d a b c x.[4] 11 0x4bdecfa9
      c <- step HH c d a b x.[7] 16 0xf6bb4b60
      b <- step HH b c d a x.[10] 23 0xbebfbc70
      a <- step HH a b c d x.[13] 4 0x289b7ec6
      d <- step HH d a b c x.[0] 11 0xeaa127fa
      c <- step HH c d a b x.[3] 16 0xd4ef3085
      b <- step HH b c d a x.[6] 23 0x4881d05
      a <- step HH a b c d x.[9] 4 0xd9d4d039
      d <- step HH d a b c x.[12] 11 0xe6db99e5
      c <- step HH c d a b x.[15] 16 0x1fa27cf8
      b <- step HH b c d a x.[2] 23 0xc4ac5665
      (a,b,c,d,x)

    let private iiTransforms (tup:int*int*int*int*int[]) =
      let mutable (a,b,c,d,x) = tup
      a <- step II a b c d x.[0] 6 0xf4292244
      d <- step II d a b c x.[7] 10 0x432aff97
      c <- step II c d a b x.[14] 15 0xab9423a7
      b <- step II b c d a x.[5] 21 0xfc93a039
      a <- step II a b c d x.[12] 6 0x655b59c3
      d <- step II d a b c x.[3] 10 0x8f0ccc92
      c <- step II c d a b x.[10] 15 0xffeff47d
      b <- step II b c d a x.[1] 21 0x85845dd1
      a <- step II a b c d x.[8] 6 0x6fa87e4f
      d <- step II d a b c x.[15] 10 0xfe2ce6e0
      c <- step II c d a b x.[6] 15 0xa3014314
      b <- step II b c d a x.[13] 21 0x4e0811a1
      a <- step II a b c d x.[4] 6 0xf7537e82
      d <- step II d a b c x.[11] 10 0xbd3af235
      c <- step II c d a b x.[2] 15 0x2ad7d2bb
      b <- step II b c d a x.[9] 21 0xeb86d391
      (a,b,c,d,x)

    let private transform (stat:MD5State) (buffer:byte[]) (shift:int) =
      let mutable tup = 
        let (a,b,c,d) = (stateToTuple stat.state)
        (a,b,c,d, (decode buffer 64 shift))

      let internalTransforms = 
        ffTransforms >> ggTransforms >> hhTransforms >> iiTransforms

      let (a,b,c,d,_) = tup |> internalTransforms

      stat.state.[0] <- stat.state.[0] + a
      stat.state.[1] <- stat.state.[1] + b
      stat.state.[2] <- stat.state.[2] + c
      stat.state.[3] <- stat.state.[3] + d
      stat

    let private internalUpdate (stat:MD5State) (buffer:byte[]) (offset:int) (length:int) =
      let mutable (index, partlen, i, start, len) = (0,0,0,0,length)

      if length - offset > (Array.length buffer) then
        len <- (Array.length buffer) - offset
      
      index <- (stat.count.[0] >>> 3) &&& 0x3f
      
      stat.count.[0] <- stat.count.[0] + (len <<< 3)
      if stat.count.[0] < (len <<< 3) then
        stat.count.[1] <- stat.count.[1] + 1
      
      stat.count.[1] <- stat.count.[1] + (len >>> 29)

      partlen <- 64 - index
      
      let mutable tStat = stat
      if (len >= partlen) then
        for k in 0..partlen do
          tStat.buffer.[k + index] <- buffer.[k + index]
        
        tStat <- transform tStat tStat.buffer 0

        i <- partlen
        while (i + 63) < len do
          tStat <- transform tStat buffer i
          i <- i + 64

        index <- 0
      else 
        i <- 0

      if i < len then
        start <- i
        for j in i..len do
          tStat.buffer.[index + j - start] <- buffer.[j + offset]

      tStat

    let updateState (stat:MD5State) (buffer:byte[]) (offset:int) (length:int) =
      internalUpdate stat buffer offset length

    let private encode (input:int[]) len =
      let mutable (i,j) = (0,0)
      let mutable out = [||] : byte[]
      let ff = 0xff

      while j < len do
        out.[j] <- byte(input.[i] &&& ff)
        out.[j + 1] <- byte((input.[i] >>> 8) &&& ff)
        out.[j + 2] <- byte((input.[i] >>> 16) &&& ff)
        out.[j + 3] <- byte((input.[i] >>> 24) &&& ff)
        j <- j + 4
        i <- i + 1

      out

    let finalState (stat:MD5State) =
      let mutable bits = [||] : byte[]
      let mutable (index, padlen) = (0,0)
      let mutable fin = { stat with count = stat.count }

      bits <- encode fin.count 8
      index <- int((fin.count.[0] >>> 3) &&& 0x3f)
      if index < 56 then
        padlen <- 56 - index
      else
        padlen <- 120 - index
        
      fin <- updateState fin padding 0 padlen
      fin <- updateState fin bits 0 8

      encode fin.state 16