package com.czifrotech.rapidtransfer.mock;

import java.io.*;
import java.util.Random;

/**
 * @author Will Czifro
 */
public class MockFilePointerGenerator {

    public static RandomAccessFile[] wrap(File file, int len, int size) {
        try {
            RandomAccessFile[] rafs = new RandomAccessFile[size];
            for (int i = 0; i < size; ++i) {
                rafs[i] = new RandomAccessFile(file, "rw");
                rafs[i].seek(i * len);
            }
            return rafs;
        } catch (IOException e) {
            e.printStackTrace();
            return null;
        }
    }
}
