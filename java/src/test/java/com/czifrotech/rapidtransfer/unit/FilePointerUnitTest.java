package com.czifrotech.rapidtransfer.unit;

import com.czifrotech.rapidtransfer.mock.MockFileGenerator;
import com.czifrotech.rapidtransfer.mock.MockFilePointerGenerator;
import com.czifrotech.rapidtransfer.simulators.FileSortingSimulator;
import org.junit.Test;
import static org.assertj.core.api.Assertions.*;

import java.io.File;
import java.io.IOException;
import java.io.RandomAccessFile;

/**
 * @author Will Czifro
 */
public class FilePointerUnitTest {

    @Test
    public void testWritingToSpecificLocation() throws IOException {
        System.out.println(System.getProperty("user.dir"));

        File file = new File("./src/test/java/samples/TestFile");
        if (!file.exists())
            fail("Failed to open file");
        RandomAccessFile raf = new RandomAccessFile(file, "rw");

        raf.seek(26);

        byte[] bytes = new byte[26];
        raf.read(bytes, 0, 26);
        String str = new String(bytes);
        System.out.println(str);

        raf.seek(26);
        raf.write(("Replaced text with this..\n").getBytes());
    }

    @Test
    public void testSortingAFile() throws IOException {
        FileSortingSimulator simulator = new FileSortingSimulator();
        simulator.testMultipleFilePointers();
    }

    @Test
    public void testMultipleFilePointers() throws IOException {
        FileSortingSimulator simulator = new FileSortingSimulator();
        simulator.testMultipleFilePointers();
    }
}
