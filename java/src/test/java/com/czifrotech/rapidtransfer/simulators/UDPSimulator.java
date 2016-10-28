package com.czifrotech.rapidtransfer.simulators;

import com.czifrotech.rapidtransfer.util.FileUtil;
import org.junit.Test;

import java.io.IOException;
import java.net.*;
import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;

/**
 * @author Will Czifro
 */
public class UDPSimulator {

    @Test
    public void singleChannelSpeedTest() throws IOException, InterruptedException {
        Map<Integer, ArrayList<Long>> allResults = new HashMap<>();
        for (int i = 0; i < 7; ++i) {
            ArrayList<Long> times = new ArrayList<>();
            for (int j = 0; j < 100; ++j) {
                long start = System.currentTimeMillis();
                singleChannel(i);
                long end = System.currentTimeMillis();
                times.add(end-start);
            }
            allResults.put(i, times);
            System.out.println("Finished 10e" + i);
        }

        ArrayList<String> output = new ArrayList<>();
        for (int key : allResults.keySet()) {
            ArrayList<Long> results = allResults.get(key);
            double mean = Calculations.mean(results);
            double stDev= Calculations.stDev(results, mean);
            String line = key + " " + mean + " " + (mean+stDev);
            output.add(line);
        }
        FileUtil.writeToFile(output, "./src/test/java/out/UDPSingleChannel_speedTest.dat");
    }

    @Test
    public void dualChannelSpeedTest() throws IOException, InterruptedException {
        Map<Integer, ArrayList<Long>> allResults = new HashMap<>();
        for (int i = 0; i < 7; ++i) {
            ArrayList<Long> times = new ArrayList<>();
            for (int j = 0; j < 100; ++j) {
                long start = System.currentTimeMillis();
                dualChannel(i);
                long end = System.currentTimeMillis();
                times.add(end-start);
            }
            allResults.put(i, times);
            System.out.println("Finished 10e" + i);
        }

        ArrayList<String> output = new ArrayList<>();
        for (int key : allResults.keySet()) {
            ArrayList<Long> results = allResults.get(key);
            double mean = Calculations.mean(results);
            double stDev= Calculations.stDev(results, mean);
            String line = key + " " + mean + " " + (mean+stDev);
            output.add(line);
        }
        FileUtil.writeToFile(output, "./src/test/java/out/UDPDualChannel_speedTest.dat");
    }

    @Test
    public void quadChannelSpeedTest() throws IOException, InterruptedException {
        Map<Integer, ArrayList<Long>> allResults = new HashMap<>();
        for (int i = 0; i < 7; ++i) {
            ArrayList<Long> times = new ArrayList<>();
            for (int j = 0; j < 100; ++j) {
                long start = System.currentTimeMillis();
                quadChannel(i);
                long end = System.currentTimeMillis();
                times.add(end-start);
            }
            allResults.put(i, times);
            System.out.println("Finished 10e" + i);
        }

        ArrayList<String> output = new ArrayList<>();
        for (int key : allResults.keySet()) {
            ArrayList<Long> results = allResults.get(key);
            double mean = Calculations.mean(results);
            double stDev= Calculations.stDev(results, mean);
            String line = key + " " + mean + " " + (mean+stDev);
            output.add(line);
        }
        FileUtil.writeToFile(output, "./src/test/java/out/UDPQuadChannel_speedTest.dat");
    }

    @Test
    public void speedTest() throws IOException, InterruptedException {
        ArrayList<String> output = new ArrayList<>();
        ArrayList<Long> times = new ArrayList<>();
        for (int i = 0; i < 100; ++i) {
            long start = System.currentTimeMillis();
            singleChannel(5);
            long end = System.currentTimeMillis();
            times.add(end-start);
        }
        double mean = Calculations.mean(times);
        double stDev = Calculations.stDev(times, mean);
        output.add(1 + " " + mean + " " + (mean+stDev));

        times.clear();
        for (int i = 0; i < 100; ++i) {
            long start = System.currentTimeMillis();
            dualChannel(5);
            long end = System.currentTimeMillis();
            times.add(end-start);
        }
        mean = Calculations.mean(times);
        stDev = Calculations.stDev(times, mean);
        output.add(2 + " " + mean + " " + (mean+stDev));

        times.clear();
        for (int i = 0; i < 100; ++i) {
            long start = System.currentTimeMillis();
            quadChannel(5);
            long end = System.currentTimeMillis();
            times.add(end-start);
        }
        mean = Calculations.mean(times);
        stDev = Calculations.stDev(times, mean);
        output.add(4 + " " + mean + " " + (mean+stDev));
        FileUtil.writeToFile(output, "./src/test/java/out/UDPSpeedTests.dat");
    }

    @Test
    public void runUDPSingleChannelTest() throws IOException, InterruptedException {
        Map<Long, ArrayList<Long>> allResults = new HashMap<>();
        for (int i = 0; i < 6; ++i) {
            ArrayList<Long> results = new ArrayList<>();
            for (int j = 0; j < 100; ++j) {
                long error = singleChannel(i);
                results.add(error);
            }
            allResults.put((long)i, results);
        }

        ArrayList<String> output = new ArrayList<>();
        for (long key : allResults.keySet()) {
            ArrayList<Long> results = allResults.get(key);
            double mean = Calculations.mean(results);
            double stDev= Calculations.stDev(results, mean);
            String line = key + " " + mean + " " + (mean+stDev);
            output.add(line);
        }
        FileUtil.writeToFile(output, "./src/test/java/out/UDPSingleChannel_errorData_maxPow6.dat");
    }

    @Test
    public void runUDPDualChannelTest() throws InterruptedException, SocketException, UnknownHostException {
        Map<Long, ArrayList<Long>> allResults = new HashMap<>();
        for (int i = 0; i < 6; ++i) {
            ArrayList<Long> results = new ArrayList<>();
            for (int j = 0; j < 100; ++j) {
                long error = dualChannel(i);
                results.add(error);
            }
            allResults.put((long)i, results);
            System.out.println("Finished 10e" + i);
        }

        ArrayList<String> output = new ArrayList<>();
        for (long key : allResults.keySet()) {
            ArrayList<Long> results = allResults.get(key);
            double mean = Calculations.mean(results);
            double stDev= Calculations.stDev(results, mean);
            String line = key + " " + mean + " " + (mean+stDev);
            output.add(line);
        }
        FileUtil.writeToFile(output, "./src/test/java/out/UDPDualChannel_errorData_maxPow6.dat");
    }

    @Test
    public void runUDPQuadChannelTest() throws InterruptedException, SocketException, UnknownHostException {
        Map<Long, ArrayList<Long>> allResults = new HashMap<>();
        for (int i = 0; i < 6; ++i) {
            ArrayList<Long> results = new ArrayList<>();
            for (int j = 0; j < 100; ++j) {
                long error = quadChannel(i);
                results.add(error);
            }
            allResults.put((long)i, results);
            System.out.println("Finished 10e" + i);
        }

        ArrayList<String> output = new ArrayList<>();
        for (long key : allResults.keySet()) {
            ArrayList<Long> results = allResults.get(key);
            double mean = Calculations.mean(results);
            double stDev= Calculations.stDev(results, mean);
            String line = key + " " + mean + " " + (mean+stDev);
            output.add(line);
        }
        FileUtil.writeToFile(output, "./src/test/java/out/UDPQuadChannel_errorData_maxPow6.dat");
    }

    public long singleChannel(int base10Pow) throws IOException, InterruptedException {
        final long limit = (int) Math.pow(10, base10Pow);
        DataStore store = new DataStore();
        DatagramSocket server = new DatagramSocket(1255);
        DatagramSocket client = new DatagramSocket();
        InetAddress IPAddress = InetAddress.getByName("localhost");
        Thread serverThread = new Thread(() -> {
            long prev = 0;
            for (long i = 0; i < limit; ++i) {
                DatagramPacket packet = new DatagramPacket(new byte[8], 8);
                try {
                    server.receive(packet);
                } catch (IOException e) {
                    long remaining = limit - i;
                    store.addMissingPacket(remaining);
                    return;
                    //e.printStackTrace();
                }
                long v = ByteTool.unwrap(packet.getData());
                if (prev == 0 && v == 0 && i == 0) {
                    store.addReceiverValue(1);
                    prev = v;
                    continue;
                }
                if (v-1 == prev)
                    store.addReceiverValue(1);
                else
                    store.addMissingPacket(prev+1);
                prev = v;
            }
        });

        serverThread.start();

        for (long i = 0; i < limit; ++i) {
            byte[] data = ByteTool.wrap(i, 8);

            DatagramPacket packet = new DatagramPacket(data, data.length, IPAddress, 1255);
            client.send(packet);
            store.addSenderValue(1);
        }

        server.close();
        client.close();
        serverThread.join();

        long senderCounter = store.getSenderCounter();
        long receiverCounter = store.getReceiverCounter();
        ArrayList<Long> missingPackets = store.getMissingPackets();

        return senderCounter - receiverCounter;
    }

    public long dualChannel(int base10Pow) throws SocketException, UnknownHostException, InterruptedException {
        final long limit = (int) Math.pow(10, base10Pow);
        int port1 = 1256, port2 = 1257;
        DataStore store = new DataStore();
        DatagramSocket[] servers = {new DatagramSocket(port1),new DatagramSocket(port2)};
        DatagramSocket[] clients = {new DatagramSocket(),new DatagramSocket()};
        InetAddress IPAddress = InetAddress.getByName("localhost");

        Thread serverThread0 = new Thread(() -> {
            long prev = 0;
            for (long i = 0; i < limit/2; ++i) {
                DatagramPacket packet = new DatagramPacket(new byte[8], 8);
                try {
                    servers[0].receive(packet);
                } catch (IOException e) {
                    long remaining = (limit/2) - i;
                    store.addMissingPacket(remaining);
                    return;
                    //e.printStackTrace();
                }
                long v = ByteTool.unwrap(packet.getData());
                if (prev == 0 && v == 0 && i == 0) {
                    store.addReceiverValue(1);
                    prev = v;
                    continue;
                }
                if (v-1 == prev)
                    store.addReceiverValue(1);
                else
                    store.addMissingPacket(prev+1);
                prev = v;
            }
        });

        Thread serverThread1 = new Thread(() -> {
            long prev = 0;
            for (long i = 0; i < limit/2; ++i) {
                DatagramPacket packet = new DatagramPacket(new byte[8], 8);
                try {
                    servers[1].receive(packet);
                } catch (IOException e) {
                    long remaining = (limit/2) - i;
                    store.addMissingPacket(remaining);
                    return;
                    //e.printStackTrace();
                }
                long v = ByteTool.unwrap(packet.getData());
                if (prev == 0 && v == 0 && i == 0) {
                    store.addReceiverValue(1);
                    prev = v;
                    continue;
                }
                if (v-1 == prev)
                    store.addReceiverValue(1);
                else
                    store.addMissingPacket(prev+1);
                prev = v;
            }
        });

        Thread clientThread0 = new Thread(() -> {
            for (long i = 0; i < limit/2; ++i) {
                try {
                    byte[] data = ByteTool.wrap(i, 8);

                    DatagramPacket packet = new DatagramPacket(data, data.length, IPAddress, port1);
                    clients[0].send(packet);
                    store.addSenderValue(1);
                } catch (IOException e) {
                    e.printStackTrace();
                }
            }
        });

        Thread clientThread1 = new Thread(() -> {
            for (long i = 0; i < limit/2; ++i) {
                try {
                    byte[] data = ByteTool.wrap(i, 8);

                    DatagramPacket packet = new DatagramPacket(data, data.length, IPAddress, port2);
                    clients[1].send(packet);
                    store.addSenderValue(1);
                } catch (IOException e) {
                    e.printStackTrace();
                }
            }
        });

        serverThread0.start();
        clientThread0.start();
        serverThread1.start();
        clientThread1.start();

        clientThread0.join();
        clients[0].close();
        servers[0].close();
        serverThread0.join();

        clientThread1.join();
        clients[1].close();
        servers[1].close();
        serverThread1.join();

        long senderCounter = store.getSenderCounter();
        long receiverCounter = store.getReceiverCounter();
        ArrayList<Long> missingPackets = store.getMissingPackets();

        return senderCounter - receiverCounter;
    }

    public long quadChannel(int base10Pow) throws SocketException, UnknownHostException, InterruptedException {
        final long limit = (int) Math.pow(10, base10Pow);
        int port1 = 1258, port2 = 1259, port3 = 1260, port4 = 1261;
        DataStore store = new DataStore();
        DatagramSocket[] servers = {new DatagramSocket(port1),new DatagramSocket(port2),
                                    new DatagramSocket(port3),new DatagramSocket(port4)};
        DatagramSocket[] clients = {new DatagramSocket(),new DatagramSocket(),
                                    new DatagramSocket(),new DatagramSocket()};
        InetAddress IPAddress = InetAddress.getByName("localhost");

        Thread serverThread0 = new Thread(() -> {
            long prev = 0;
            for (long i = 0; i < limit/4; ++i) {
                DatagramPacket packet = new DatagramPacket(new byte[8], 8);
                try {
                    servers[0].receive(packet);
                } catch (IOException e) {
                    long remaining = (limit/4) - i;
                    store.addMissingPacket(remaining);
                    return;
                    //e.printStackTrace();
                }
                long v = ByteTool.unwrap(packet.getData());
                if (prev == 0 && v == 0 && i == 0) {
                    store.addReceiverValue(1);
                    prev = v;
                    continue;
                }
                if (v-1 == prev)
                    store.addReceiverValue(1);
                else
                    store.addMissingPacket(prev+1);
                prev = v;
            }
        });

        Thread serverThread1 = new Thread(() -> {
            long prev = 0;
            for (long i = 0; i < limit/4; ++i) {
                DatagramPacket packet = new DatagramPacket(new byte[8], 8);
                try {
                    servers[1].receive(packet);
                } catch (IOException e) {
                    long remaining = (limit/4) - i;
                    store.addMissingPacket(remaining);
                    return;
                    //e.printStackTrace();
                }
                long v = ByteTool.unwrap(packet.getData());
                if (prev == 0 && v == 0 && i == 0) {
                    store.addReceiverValue(1);
                    prev = v;
                    continue;
                }
                if (v-1 == prev)
                    store.addReceiverValue(1);
                else
                    store.addMissingPacket(prev+1);
                prev = v;
            }
        });

        Thread serverThread2 = new Thread(() -> {
            long prev = 0;
            for (long i = 0; i < limit/4; ++i) {
                DatagramPacket packet = new DatagramPacket(new byte[8], 8);
                try {
                    servers[2].receive(packet);
                } catch (IOException e) {
                    long remaining = (limit/4) - i;
                    store.addMissingPacket(remaining);
                    return;
                    //e.printStackTrace();
                }
                long v = ByteTool.unwrap(packet.getData());
                if (prev == 0 && v == 0 && i == 0) {
                    store.addReceiverValue(1);
                    prev = v;
                    continue;
                }
                if (v-1 == prev)
                    store.addReceiverValue(1);
                else
                    store.addMissingPacket(prev+1);
                prev = v;
            }
        });

        Thread serverThread3 = new Thread(() -> {
            long prev = 0;
            for (long i = 0; i < limit/4; ++i) {
                DatagramPacket packet = new DatagramPacket(new byte[8], 8);
                try {
                    servers[3].receive(packet);
                } catch (IOException e) {
                    long remaining = (limit/4) - i;
                    store.addMissingPacket(remaining);
                    return;
                    //e.printStackTrace();
                }
                long v = ByteTool.unwrap(packet.getData());
                if (prev == 0 && v == 0 && i == 0) {
                    store.addReceiverValue(1);
                    prev = v;
                    continue;
                }
                if (v-1 == prev)
                    store.addReceiverValue(1);
                else
                    store.addMissingPacket(prev+1);
                prev = v;
            }
        });

        Thread clientThread0 = new Thread(() -> {
            for (long i = 0; i < limit/4; ++i) {
                try {
                    byte[] data = ByteTool.wrap(i, 8);

                    DatagramPacket packet = new DatagramPacket(data, data.length, IPAddress, port1);
                    clients[0].send(packet);
                    store.addSenderValue(1);
                } catch (IOException e) {
                    e.printStackTrace();
                }
            }
        });

        Thread clientThread1 = new Thread(() -> {
            for (long i = 0; i < limit/4; ++i) {
                try {
                    byte[] data = ByteTool.wrap(i, 8);

                    DatagramPacket packet = new DatagramPacket(data, data.length, IPAddress, port2);
                    clients[1].send(packet);
                    store.addSenderValue(1);
                } catch (IOException e) {
                    e.printStackTrace();
                }
            }
        });

        Thread clientThread2 = new Thread(() -> {
            for (long i = 0; i < limit/4; ++i) {
                try {
                    byte[] data = ByteTool.wrap(i, 8);

                    DatagramPacket packet = new DatagramPacket(data, data.length, IPAddress, port3);
                    clients[2].send(packet);
                    store.addSenderValue(1);
                } catch (IOException e) {
                    e.printStackTrace();
                }
            }
        });

        Thread clientThread3 = new Thread(() -> {
            for (long i = 0; i < limit/4; ++i) {
                try {
                    byte[] data = ByteTool.wrap(i, 8);

                    DatagramPacket packet = new DatagramPacket(data, data.length, IPAddress, port4);
                    clients[3].send(packet);
                    store.addSenderValue(1);
                } catch (IOException e) {
                    e.printStackTrace();
                }
            }
        });

        serverThread0.start();
        clientThread0.start();
        serverThread1.start();
        clientThread1.start();
        serverThread2.start();
        clientThread2.start();
        serverThread3.start();
        clientThread3.start();

        clientThread0.join();
        clients[0].close();
        servers[0].close();
        serverThread0.join();

        clientThread1.join();
        clients[1].close();
        servers[1].close();
        serverThread1.join();

        clientThread2.join();
        clients[2].close();
        servers[2].close();
        serverThread2.join();

        clientThread3.join();
        clients[3].close();
        servers[3].close();
        serverThread3.join();

        long senderCounter = store.getSenderCounter();
        long receiverCounter = store.getReceiverCounter();
        ArrayList<Long> missingPackets = store.getMissingPackets();

        return senderCounter - receiverCounter;
    }

    private class DataStore {
        private final Object locker1 = new Object(), locker2 = new Object(), locker3 = new Object();

        private long senderCounter, receiverCounter;

        private ArrayList<Long> missingPackets = new ArrayList<>();

        public void addSenderValue(long val) {
            synchronized (locker1) {
                senderCounter += val;
            }
        }

        public void addMissingPacket(long packet) {
            synchronized (locker3) {
                missingPackets.add(packet);
            }
        }

        public void addReceiverValue(long val) {
            synchronized (locker2) {
                receiverCounter += val;
            }
        }

        public long getSenderCounter() {
            return senderCounter;
        }

        public long getReceiverCounter() {
            return receiverCounter;
        }

        public ArrayList<Long> getMissingPackets() {
            return missingPackets;
        }
    }

    private static class ByteTool {
        public static byte[] wrap(long v, int size) {
            return ByteBuffer.allocate(size).putLong(v).array();
        }

        public static long unwrap(byte[] b) {
            return ByteBuffer.wrap(b).getLong();
        }
    }

    private static class Calculations {
        public static double mean(ArrayList<Long> values) {
            long sum = 0;
            for (long v : values)
                sum += v;
            return ((double)sum)/((double)values.size());
        }

        public static double stDev(ArrayList<Long> values, double mean) {
            double sum = 0;
            for (long v : values)
                sum += Math.pow((((double)v)-mean), 2);
            return Math.sqrt(sum / (((double)values.size())-1));
        }
    }
}
