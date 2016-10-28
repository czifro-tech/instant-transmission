package com.czifrotech.rapidtransfer.simulators;

import com.czifrotech.rapidtransfer.mock.MockFileGenerator;
import com.czifrotech.rapidtransfer.mock.MockFilePointerGenerator;
import com.czifrotech.rapidtransfer.util.FileUtil;
import org.junit.Test;

import java.io.File;
import java.io.IOException;
import java.io.RandomAccessFile;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;

/**
 * @author Will Czifro
 */
public class FileSortingSimulator {

    private int size = 6;

    @Test
    public void runSinglePointerSimulator() throws IOException {
        Map<Integer, ArrayList<Result>> allResults = new HashMap<>();
        for (size = 0; size < 6; ++size) {
            ArrayList<Result> results = new ArrayList<>();
            for (int i = 0; i < 100; ++i) {
                results.add(testSortingAFile());
                System.gc();
            }
            allResults.put(size, results);
        }

        ArrayList<String> output = new ArrayList<>();
        for (int key : allResults.keySet()) {
            ArrayList<Result> results = allResults.get(key);
            double mean = Calculations.mean(results);
            double stDev = Calculations.stDev(results, mean);
            output.add(key + " " + mean + " " + (mean+stDev));
        }
        FileUtil.writeToFile(output, "./src/test/java/out/SingleFilePointer.dat");
    }

    @Test
    public void runMultiplePointerSimulator() throws IOException {
        Map<Integer, ArrayList<Result>> allResults = new HashMap<>();
        for (size = 0; size < 4; ++size) {
            ArrayList<Result> results = new ArrayList<>();
            for (int i = 0; i < 100; ++i) {
                results.add(testMultipleFilePointers());
                System.gc();
            }
            allResults.put(size, results);
        }

        ArrayList<String> output = new ArrayList<>();
        for (int key : allResults.keySet()) {
            ArrayList<Result> results = allResults.get(key);
            double mean = Calculations.mean(results);
            double stDev = Calculations.stDev(results, mean);
            output.add(key + " " + mean + " " + (mean+stDev));
        }
        FileUtil.writeToFile(output, "./src/test/java/out/MultipleFilePointers.dat");
    }

    public Result testSortingAFile() throws IOException {
        Runtime curRuntime = Runtime.getRuntime();
        long freeMem = curRuntime.freeMemory();
        //int size = 3; // +2 for newline
        int[] unsortedSegmentIds =
                MockFileGenerator.createFileAndReturnContentsAsIntegers(MockFileGenerator.outOfOrderFileSorted, size);
        File file = new File(MockFileGenerator.outOfOrderFileSorted);
        RandomAccessFile raf = new RandomAccessFile(file, "rw");

        long start = System.nanoTime();
        int i = 0;
        while(true) {
            int j = unsortedSegmentIds[i]-1;
            int curPos = i * (size+2);
            int swapPos = j * (size+2);

            if (i == j) {
                i++;
                if (i >= unsortedSegmentIds.length)
                    break;
                continue;
            }

            raf.seek(curPos);
            byte[] curBytes = new byte[size+1];
            raf.read(curBytes);

            raf.seek(swapPos);
            byte[] swapBytes = new byte[size+1];
            raf.read(swapBytes);

//            String cur = new String(curBytes);
//            String swap = new String(swapBytes);
//
//            if (cur.charAt(0) == '\0' || swap.charAt(0) == '\0') {
//                swap = swap;
//            }
//
//            System.out.println(cur);
//            System.out.println(swap);

            unsortedSegmentIds[i] = unsortedSegmentIds[j];
            unsortedSegmentIds[j] = j+1;

            raf.seek(swapPos);
            raf.write(curBytes);

            raf.seek(curPos);
            raf.write(swapBytes);
        }
        long end = System.nanoTime();

        long newFreeMem = curRuntime.freeMemory();
        raf.close();
//        System.out.println("Free Mem: " + (freeMem - newFreeMem) + " bytes");
//        System.out.println(((double)(end-start))/1000000 + "ms");
        Result ret = new Result();
        ret.memoryUsed = (freeMem - newFreeMem);
        ret.timeTaken = ((double)(end-start))/1000000;
        return ret;
    }

    public Result testMultipleFilePointers() throws IOException {
        Runtime curRuntime = Runtime.getRuntime();
        long freeMem = curRuntime.freeMemory();
        //int size = 3;
        int[] unsortedSegmentIds =
                MockFileGenerator.createFileAndReturnContentsAsIntegers(MockFileGenerator.outOfOrderFileSorted, size);
        File file = new File(MockFileGenerator.outOfOrderFileSorted);
        RandomAccessFile[] r_rafs = MockFilePointerGenerator.wrap(file, size+2, unsortedSegmentIds.length);
        RandomAccessFile[] w_rafs = MockFilePointerGenerator.wrap(file, size+2, unsortedSegmentIds.length);

        long start = System.nanoTime();
        int i = 0;
        while (true) {
            int j = unsortedSegmentIds[i]-1;

            if (i == j) {
                i++;
                if (i >= unsortedSegmentIds.length)
                    break;
                continue;
            }
            long curPos = r_rafs[i].getFilePointer();
            byte[] curBytes = new byte[size+1];
            r_rafs[i].read(curBytes);

            byte[] swapBytes = new byte[size+1];
            r_rafs[j].read(swapBytes);

            //String cur = new String(curBytes);
            //String swap = new String(swapBytes);

            //System.out.println(cur);
            //System.out.println(swap);

            unsortedSegmentIds[i] = unsortedSegmentIds[j];
            unsortedSegmentIds[j] = j+1;

            w_rafs[i].write(swapBytes);

            w_rafs[j].write(curBytes);

            r_rafs[i].seek(curPos);
            w_rafs[i].seek(curPos);
        }
        long end = System.nanoTime();
        long newFreeMem = curRuntime.freeMemory();

        // closing streams outside of timed scope
        // because we want to compare File I/O with many seeks
        // to File I/O with a few seeks.
        for (i = 0; i < r_rafs.length; ++i) {
            r_rafs[i].close();
            w_rafs[i].close();
        }

//        System.out.println("Free Mem: " + (freeMem - newFreeMem) + " bytes");
//        System.out.println(((double)(end-start))/1000000 + "ms");
        Result ret = new Result();
        ret.memoryUsed = (freeMem - newFreeMem);
        ret.timeTaken = ((double)(end-start))/1000000;
        return ret;
    }

    private class Result {
        public long memoryUsed = 0;
        public double timeTaken = 0;
    }

    private static class Calculations {
        public static double mean(ArrayList<Result> values) {
            long sum = 0;
            for (Result v : values)
                sum += v.timeTaken;
            return ((double)sum)/((double)values.size());
        }

        public static double stDev(ArrayList<Result> values, double mean) {
            double sum = 0;
            for (Result v : values)
                sum += Math.pow((((double)v.timeTaken)-mean), 2);
            return Math.sqrt(sum / (((double)values.size())-1));
        }
    }
}
