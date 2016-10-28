package com.czifrotech.rapidtransfer.mock;

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.util.Random;

/**
 * @author Will Czifro
 */
public class MockFileGenerator {

    private static String outOfOrderFile = "./src/test/java/samples/OutOfOrderFile.txt";
    public static String outOfOrderFileSorted = "./src/test/java/samples/OutOfOrderFileSorted.txt";

    public static void createFile(String filename, int base10Pow) {
        createFileAndReturnContentsAsIntegers(filename, base10Pow);
    }

    public static int[] createFileAndReturnContentsAsIntegers(String filename, int base10Pow) {
        try {
            int[] data = generateIntegers(base10Pow);
            File file1 = new File(filename);
            File file2 = new File(outOfOrderFile); // backup file for comparison
            if (file1.exists())
                file1.delete();
            if (file2.exists())
                file2.delete();

            BufferedWriter writer1 = new BufferedWriter(new FileWriter(file1));
            BufferedWriter writer2 = new BufferedWriter(new FileWriter(file2));
            for (int d : data) {
                writer1.write(toPaddedStringAsCharArray(d, base10Pow+2)); // +2 for \n
                writer2.write(toPaddedStringAsCharArray(d, base10Pow+2)); // +2 for \n
            }
            writer1.close();
            writer2.close();
            return data;
        } catch (IOException e) {
            e.printStackTrace();
        }
        return null;
    }

    private static int[] generateIntegers(int base10Pow) {
        int size = (int) Math.pow(10, base10Pow);
        int[] arr = new int[size];

        for (int i = 1; i <= size; ++i) {
            arr[i - 1] = i;
        }

        Random rand = new Random();
        for (int i = arr.length - 1; i > 0; --i) {
            int index = rand.nextInt(i + 1);

            if (arr[index] == 10)
                index = index;
            int t = arr[index];
            arr[index] = arr[i];
            arr[i] = t;
        }

        return arr;
    }

    private static char[] toPaddedStringAsCharArray(int i, int maxPadSize) {
        String str = Integer.toString(i);
        char[] chars = new char[maxPadSize];

        for (int j = 0; j < chars.length; ++j) {
            if (j > str.length()-1)
                chars[j] = ' ';
            else
                chars[j] = str.charAt(j);
        }

        chars[chars.length-1] = '\n';

        return chars;
    }
}
