/**
 * Benchmark of Reed-Solomon encoding.
 *
 * Copyright 2015, Backblaze, Inc.  All rights reserved.
 * Copyright © 2019 Natalia Portillo
 */

using System;
using System.Collections.Generic;
using System.Text;
using Claunia.ReedSolomon;

namespace ReedSolomonBenchmark
{
    internal sealed class ReedSolomonBenchmark
    {
        const int  DATA_COUNT                 = 17;
        const int  PARITY_COUNT               = 3;
        const int  TOTAL_COUNT                = DATA_COUNT + PARITY_COUNT;
        const int  BUFFER_SIZE                = 200       * 1000;
        const int  PROCESSOR_CACHE_SIZE       = 10 * 1024 * 1024;
        const int  TWICE_PROCESSOR_CACHE_SIZE = 2         * PROCESSOR_CACHE_SIZE;
        const int  NUMBER_OF_BUFFER_SETS      = TWICE_PROCESSOR_CACHE_SIZE / DATA_COUNT / BUFFER_SIZE + 1;
        const long MEASUREMENT_DURATION       = 2 * 1000;

        static readonly Random Random = new Random();

        int nextBuffer;

        internal void Run()
        {
            Console.WriteLine("preparing...");
            BufferSet[] bufferSets = new BufferSet [NUMBER_OF_BUFFER_SETS];

            for(int iBufferSet = 0; iBufferSet < NUMBER_OF_BUFFER_SETS; iBufferSet++)
                bufferSets[iBufferSet] = new BufferSet();

            byte[] tempBuffer = new byte [BUFFER_SIZE];

            List<string> summaryLines = new List<string>();
            var          csv          = new StringBuilder();
            csv.Append("Outer,Middle,Inner,Multiply,Encode,Check\n");

            foreach(ICodingLoop codingLoop in CodingLoopBase.ALL_CODING_LOOPS)
            {
                var encodeAverage = new Measurement();

                {
                    string testName = codingLoop.GetType().Name + " encodeParity";
                    Console.WriteLine("\nTEST: "                + testName);
                    var codec = new ReedSolomon(DATA_COUNT, PARITY_COUNT, codingLoop);
                    Console.WriteLine("    warm up...");
                    DoOneEncodeMeasurement(codec, bufferSets);
                    DoOneEncodeMeasurement(codec, bufferSets);
                    Console.WriteLine("    testing...");

                    for(int iMeasurement = 0; iMeasurement < 10; iMeasurement++)
                        encodeAverage.Add(DoOneEncodeMeasurement(codec, bufferSets));

                    Console.WriteLine("\nAVERAGE: {0}", encodeAverage);
                    summaryLines.Add($"    {testName,-45} {encodeAverage}");
                }

                // The encoding test should have filled all of the buffers with
                // correct parity, so we can benchmark parity checking.
                var checkAverage = new Measurement();

                {
                    string testName = codingLoop.GetType().Name + " isParityCorrect";
                    Console.WriteLine("\nTEST: "                + testName);
                    var codec = new ReedSolomon(DATA_COUNT, PARITY_COUNT, codingLoop);
                    Console.WriteLine("    warm up...");
                    DoOneEncodeMeasurement(codec, bufferSets);
                    DoOneEncodeMeasurement(codec, bufferSets);
                    Console.WriteLine("    testing...");

                    for(int iMeasurement = 0; iMeasurement < 10; iMeasurement++)
                        checkAverage.Add(DoOneCheckMeasurement(codec, bufferSets, tempBuffer));

                    Console.WriteLine("\nAVERAGE: {0}", checkAverage);
                    summaryLines.Add($"    {testName,-45} {checkAverage}");
                }

                csv.Append(CodingLoopNameToCsvPrefix(codingLoop.GetType().Name));
                csv.Append(encodeAverage.GetRate());
                csv.Append(",");
                csv.Append(checkAverage.GetRate());
                csv.Append("\n");
            }

            Console.WriteLine("\n");
            Console.WriteLine(csv.ToString());

            Console.WriteLine("\nSummary:\n");

            foreach(string line in summaryLines)
                Console.WriteLine(line);
        }

        Measurement DoOneEncodeMeasurement(ReedSolomon codec, BufferSet[] bufferSets)
        {
            long passesCompleted = 0;
            long bytesEncoded    = 0;
            long encodingTime    = 0;

            while(encodingTime < MEASUREMENT_DURATION)
            {
                BufferSet bufferSet = bufferSets[nextBuffer];
                nextBuffer = (nextBuffer + 1) % bufferSets.Length;
                byte[][] shards    = bufferSet.Buffers;
                DateTime startTime = DateTime.UtcNow;
                codec.EncodeParity(shards, 0, BUFFER_SIZE);
                DateTime endTime = DateTime.UtcNow;
                encodingTime    += (long)(endTime - startTime).TotalMilliseconds;
                bytesEncoded    += BUFFER_SIZE * DATA_COUNT;
                passesCompleted += 1;
            }

            double seconds   = encodingTime / 1000.0;
            double megabytes = bytesEncoded / 1000000.0;
            var    result    = new Measurement(megabytes, seconds);
            Console.WriteLine("        {0} passes, {1}", passesCompleted, result);

            return result;
        }

        Measurement DoOneCheckMeasurement(ReedSolomon codec, BufferSet[] bufferSets, byte[] tempBuffer)
        {
            long passesCompleted = 0;
            long bytesChecked    = 0;
            long checkingTime    = 0;

            while(checkingTime < MEASUREMENT_DURATION)
            {
                BufferSet bufferSet = bufferSets[nextBuffer];
                nextBuffer = (nextBuffer + 1) % bufferSets.Length;
                byte[][] shards    = bufferSet.Buffers;
                DateTime startTime = DateTime.UtcNow;

                if(!codec.IsParityCorrect(shards, 0, BUFFER_SIZE, tempBuffer))
                    throw new Exception("parity not correct");

                DateTime endTime = DateTime.UtcNow;
                checkingTime    += (long)(endTime - startTime).TotalMilliseconds;
                bytesChecked    += BUFFER_SIZE * DATA_COUNT;
                passesCompleted += 1;
            }

            double seconds   = checkingTime / 1000.0;
            double megabytes = bytesChecked / 1000000.0;
            var    result    = new Measurement(megabytes, seconds);
            Console.WriteLine("        {0} passes, {1}", passesCompleted, result);

            return result;
        }

        /// <summary>Converts a name like "OutputByteInputTableCodingLoop" to "output,byte,input,table,".</summary>
        static string CodingLoopNameToCsvPrefix(string className)
        {
            List<string> names = SplitCamelCase(className);

            return names[0] + "," + names[1] + "," + names[2] + "," + names[3] + ",";
        }

        /// <summary>
        ///     Converts a name like "OutputByteInputTableCodingLoop" to a List of words: { "output", "byte", "input",
        ///     "table", "coding", "loop" }
        /// </summary>
        /// <param name="className"></param>
        /// <returns></returns>
        static List<string> SplitCamelCase(string className)
        {
            string       remaining = className;
            List<string> result    = new List<string>();

            while(!string.IsNullOrEmpty(remaining))
            {
                bool found = false;

                for(int i = 1; i < remaining.Length; i++)
                    if(char.IsUpper(remaining[i]))
                    {
                        result.Add(remaining.Substring(0, i));
                        remaining = remaining.Substring(i);
                        found     = true;

                        break;
                    }

                if(found)
                    continue;

                result.Add(remaining);
                remaining = "";
            }

            return result;
        }

        sealed class BufferSet
        {
            public readonly byte[][] Buffers;

            public BufferSet()
            {
                Buffers = new byte [TOTAL_COUNT][];

                for(int iBuffer = 0; iBuffer < TOTAL_COUNT; iBuffer++)
                {
                    Buffers[iBuffer] = new byte[BUFFER_SIZE];
                    byte[] buffer = Buffers[iBuffer];

                    for(int iByte = 0; iByte < BUFFER_SIZE; iByte++)
                        buffer[iByte] = (byte)Random.Next(256);
                }

                byte[] bigBuffer = new byte [TOTAL_COUNT * BUFFER_SIZE];

                for(int i = 0; i < TOTAL_COUNT * BUFFER_SIZE; i++)
                    bigBuffer[i] = (byte)Random.Next(256);
            }
        }

        sealed class Measurement
        {
            double megabytes;
            double seconds;

            public Measurement()
            {
                megabytes = 0.0;
                seconds   = 0.0;
            }

            public Measurement(double megabytes, double seconds)
            {
                this.megabytes = megabytes;
                this.seconds   = seconds;
            }

            public void Add(Measurement other)
            {
                megabytes += other.megabytes;
                seconds   += other.seconds;
            }

            public double GetRate() => megabytes / seconds;

            public override string ToString() => $"{GetRate():F1} MB/s";
        }
    }
}