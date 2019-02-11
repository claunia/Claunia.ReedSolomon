/**
 * One specific ordering/nesting of the coding loops.
 *
 * Copyright 2015, Backblaze, Inc.  All rights reserved.
 * Copyright © 2019 Natalia Portillo
 */

namespace Claunia.ReedSolomon
{
    public class OutputByteInputTableCodingLoop : CodingLoopBase
    {
        public override void CodeSomeShards(byte[][] matrixRows, byte[][] inputs, int inputCount, byte[][] outputs,
                                            int outputCount, int offset, int byteCount)
        {
            byte[][] table = Galois.MULTIPLICATION_TABLE;

            for(int iOutput = 0; iOutput < outputCount; iOutput++)
            {
                byte[] outputShard = outputs[iOutput];
                byte[] matrixRow   = matrixRows[iOutput];

                for(int iByte = offset; iByte < offset + byteCount; iByte++)
                {
                    int value = 0;

                    for(int iInput = 0; iInput < inputCount; iInput++)
                    {
                        byte[] inputShard   = inputs[iInput];
                        byte[] multTableRow = table[matrixRow[iInput] & 0xFF];
                        value ^= multTableRow[inputShard[iByte] & 0xFF];
                    }

                    outputShard[iByte] = (byte)value;
                }
            }
        }
    }
}