/**
 * One specific ordering/nesting of the coding loops.
 *
 * Copyright 2015, Backblaze, Inc.  All rights reserved.
 * Copyright © 2019 Natalia Portillo
 */

namespace Claunia.ReedSolomon
{
    public class InputByteOutputTableCodingLoop : CodingLoopBase
    {
        public override void CodeSomeShards(byte[][] matrixRows, byte[][] inputs, int inputCount, byte[][] outputs,
                                            int outputCount, int offset, int byteCount)
        {
            byte[][] table = Galois.MULTIPLICATION_TABLE;

            {
                int    iInput     = 0;
                byte[] inputShard = inputs[iInput];

                for(int iByte = offset; iByte < offset + byteCount; iByte++)
                {
                    byte   inputByte    = inputShard[iByte];
                    byte[] multTableRow = table[inputByte & 0xFF];

                    for(int iOutput = 0; iOutput < outputCount; iOutput++)
                    {
                        byte[] outputShard = outputs[iOutput];
                        byte[] matrixRow   = matrixRows[iOutput];
                        outputShard[iByte] = multTableRow[matrixRow[iInput] & 0xFF];
                    }
                }
            }

            for(int iInput = 1; iInput < inputCount; iInput++)
            {
                byte[] inputShard = inputs[iInput];

                for(int iByte = offset; iByte < offset + byteCount; iByte++)
                {
                    byte   inputByte    = inputShard[iByte];
                    byte[] multTableRow = table[inputByte & 0xFF];

                    for(int iOutput = 0; iOutput < outputCount; iOutput++)
                    {
                        byte[] outputShard = outputs[iOutput];
                        byte[] matrixRow   = matrixRows[iOutput];
                        outputShard[iByte] ^= multTableRow[matrixRow[iInput] & 0xFF];
                    }
                }
            }
        }
    }
}