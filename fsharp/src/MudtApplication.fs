namespace MUDT

  type MudtAppMode =
    | Stream = 0
    | Ftp    = 1

  type MudtAppAsynchrony =
    | Parallel   = 0
    | Parallelx2 = 1
    | Parallelx3 = 2

  type MudtAppMemoryUsage =
    | Full    = 0
    | Half    = 1
    | Quarter = 2
    | Custom  = 3

  type MudtAppConfiguration =
    {
      mode : MudtAppMode
      asynchrony : MudtAppAsynchrony
      memUsage : MudtAppMemoryUsage
    }

  module MudtApp =

    open MUDT.Diagnostics

    let startApp (config:MudtAppConfiguration) =
      Cache.cacheItem "mode" (int(config.mode))
      Cache.cacheItem "asynchrony" (int(config.asynchrony))
      Cache.cacheItem "memUsage" (int(config.memUsage))
      0