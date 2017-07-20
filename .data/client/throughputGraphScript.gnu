set style line 1 lt 1 lw 3 pt 3 linecolor rgb "red"
set style line 2 lt 1 lw 3 pt 3 linecolor rgb "green"
set style line 3 lt 1 lw 3 pt 3 linecolor rgb "blue"
set key left
set xlabel "Time (seconds)"
set ylabel "Throughput (bytes)"
set title "Client: MCDTP Network Throughput"

plot "single-channel-1400B-transfer-1GB.dat" using 1:2 with lines title "Single",\
     "dual-channel-1400B-transfer-1GB.dat" using 1:2 with lines title "Dual",\
     "quad-channel-1400B-transfer-1GB.dat" using 1:2 with lines title "Quad"