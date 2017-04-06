#!/usr/bin/env bash
if test "$OS" = "Windows_NT"
then
  # use .Net
  # todo add support for win
  echo "Windows support unavailable..."
else
  fsharpi ./thesis/build.fsx 
fi
