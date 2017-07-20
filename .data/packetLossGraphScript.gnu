set title "Packet Loss"
unset key
# set key inside right top vertical Right noreverse noenhanced autotitle nobox
# Make the x axis labels easier to read.
set xtics rotate out
# Select histogram data
set style data histogram
# Give the bars a plain fill pattern, and draw a solid line around them.
set style fill solid border
set style histogram clustered
set xlabel "Channel Count"
set ylabel "Packet Loss Percentage"
plot for [COL=2:3] 'packet-loss-percentage.dat' using COL:xticlabels(1)