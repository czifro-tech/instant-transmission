set style line 1 lt 1 lw 3 pt 3 linecolor rgb "red"
set style line 2 lt 1 lw 3 pt 3 linecolor rgb "green"
set style line 3 lt 1 lw 3 pt 3 linecolor rgb "blue"
set key left
set xlabel "Packet Loss"
set ylabel "Packets Transmitted (10^y)"
set title "Error Test"

plot "out/UDPSingleChannel_errorData_maxPow6.dat" using 2:1 with lines title "Single Channel",\
     "out/UDPQuadChannel_errorData_maxPow6.dat" using 2:1 with lines title "Quad Channel"