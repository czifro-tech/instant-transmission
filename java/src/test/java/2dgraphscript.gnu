set style line 1 lt 1 lw 3 pt 3 linecolor rgb "red"
set style line 2 lt 1 lw 3 pt 3 linecolor rgb "green"
set style line 3 lt 1 lw 3 pt 3 linecolor rgb "blue"
set key left
set xlabel "Time (ms)"
set ylabel "Packets Sent (10^y)"
set title "Speed Test"

plot "out/UDPSingleChannel_speedTest.dat" using 2:1 with lines title "Single Channel",\
     "out/UDPQuadChannel_speedTest.dat" using 2:1 with lines title "Quad Channel"