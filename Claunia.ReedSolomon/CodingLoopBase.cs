/**
 * Common implementations for coding loops.
 *
 * Copyright 2015, Backblaze, Inc.  All rights reserved.
 * Copyright © 2019 Natalia Portillo
 */

using System.Diagnostics.CodeAnalysis;

namespace Claunia.ReedSolomon
{
    public abstract class CodingLoopBase : ICodingLoop
    {
        /// <summary>
        ///     All of the available coding loop algorithms. The different choices nest the three loops in different orders,
        ///     and either use the log/exponents tables, or use the multiplication table. The naming of the three loops is (with
        ///     number of loops in benchmark): "byte"   - Index of byte within shard.  (200,000 bytes in each shard) "input"  -
        ///     Which input shard is being read.  (17 data shards) "output"  - Which output shard is being computed.  (3 parity
        ///     shards) And the naming for multiplication method is: "table"  - Use the multiplication table. "exp"    - Use the
        ///     logarithm/exponent table. The ReedSolomonBenchmark class compares the performance of the different loops, which
        ///     will depend on the specific processor you're running on. This is the inner loop.  It needs to be fast.  Be careful
        ///     if you change it. I have tried inlining Galois.multiply(), but it doesn't make things any faster.  The JIT compiler
        ///     is known to inline methods, so it's probably already doing so.
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static readonly ICodingLoop[] ALL_CODING_LOOPS =
        {
            /*new ByteInputOutputExpCodingLoop(), new ByteInputOutputTableCodingLoop(),
            new ByteOutputInputExpCodingLoop(), new ByteOutputInputTableCodingLoop(),
            new InputByteOutputExpCodingLoop(), new InputByteOutputTableCodingLoop(),
            new InputOutputByteExpCodingLoop(), new InputOutputByteTableCodingLoop(),
            new OutputByteInputExpCodingLoop(), new OutputByteInputTableCodingLoop(),
            new OutputInputByteExpCodingLoop(), new OutputInputByteTableCodingLoop()*/
        };

        public abstract void CodeSomeShards(byte[][] matrixRows, byte[][] inputs, int inputCount, byte[][] outputs,
                                            int outputCount, int offset, int byteCount);

        public virtual bool CheckSomeShards(byte[][] matrixRows, byte[][] inputs, int inputCount, byte[][] toCheck,
                                            int checkCount, int offset, int byteCount, byte[] tempBuffer)
        {
            // This is the loop structure for ByteOutputInput, which does not
            // require temporary buffers for checking.
            byte[][] table = Galois.MULTIPLICATION_TABLE;

            for(int iByte = offset; iByte < offset + byteCount; iByte++)
            {
                for(int iOutput = 0; iOutput < checkCount; iOutput++)
                {
                    byte[] matrixRow = matrixRows[iOutput];
                    int    value     = 0;

                    for(int iInput = 0; iInput < inputCount; iInput++)
                        value ^= table[matrixRow[iInput] & 0xFF][inputs[iInput][iByte] & 0xFF];

                    if(toCheck[iOutput][iByte] != (byte)value)
                        return false;
                }
            }

            return true;
        }
    }
}