#/usr/bin/env bash
fileSuffix="-channel-1400B-transfer-1GB.raw"
fileSize=1119371976
c1='client/single'$fileSuffix
c2='client/dual'$fileSuffix
c3='client/quad'$fileSuffix

process(){
  recvd=$(cat $1 | egrep '\d+:\d+:\d+.\d+\s\d+' | sed -n -e 's/^.* //p' | awk '{ sum += $1} END { print sum }')
  echo $2: $recvd
}
process $c1 'Single'
process $c2 'Dual'
process $c3 'Quad'