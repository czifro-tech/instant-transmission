set title "Average Throughput"
set key inside right top vertical Right noreverse noenhanced autotitle nobox
# Make the x axis labels easier to read.
set xtics rotate out
# Select histogram data
set style data histogram
# Give the bars a plain fill pattern, and draw a solid line around them.
set style fill solid border
set style histogram clustered
set xlabel "Channel Count"
set ylabel "Throughput (bytes)"
plot for [COL=2:3] 'average-throughputs.dat' using COL:xticlabels(1) title columnheader