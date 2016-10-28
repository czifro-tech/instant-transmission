package com.czifrotech.rapidtransfer.util;

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.util.ArrayList;

/**
 * @author Will Czifro
 */
public class FileUtil {

    public static void writeToFile(ArrayList<String> output, String filename) {
        try {
            File file = new File(filename);
            if (file.exists())
                file.delete();
            BufferedWriter writer = new BufferedWriter(new FileWriter(file));
            for (String line : output) {
                writer.write(line);
                if (!line.contains("\n"))
                    writer.newLine();
            }
            writer.close();
        } catch (IOException e) {
            e.printStackTrace();
        }
    }
}
