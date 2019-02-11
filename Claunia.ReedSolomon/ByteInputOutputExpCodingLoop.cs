/**
 * One specific ordering/nesting of the coding loops.
 *
 * Copyright 2015, Backblaze, Inc.  All rights reserved.
 * Copyright © 2019 Natalia Portillo
 */

namespace Claunia.ReedSolomon
{
    public class ByteInputOutputExpCodingLoop : CodingLoopBase
    {
        public override void CodeSomeShards(byte[][] matrixRows, byte[][] inputs, int inputCount, byte[][] outputs,
                                            int outputCount, int offset, int byteCount)
        {
            for(int iByte = offset; iByte < offset + byteCount; iByte++)
            {
                {
                    int    iInput     = 0;
                    byte[] inputShard = inputs[iInput];
                    byte   inputByte  = inputShard[iByte];

                    for(int iOutput = 0; iOutput < outputCount; iOutput++)
                    {
                        byte[] outputShard = outputs[iOutput];
                        byte[] matrixRow   = matrixRows[iOutput];
                        outputShard[iByte] = Galois.Multiply(matrixRow[iInput], inputByte);
                    }
                }

                for(int iInput = 1; iInput < inputCount; iInput++)
                {
                    byte[] inputShard = inputs[iInput];
                    byte   inputByte  = inputShard[iByte];

                    for(int iOutput = 0; iOutput < outputCount; iOutput++)
                    {
                        byte[] outputShard = outputs[iOutput];
                        byte[] matrixRow   = matrixRows[iOutput];
                        outputShard[iByte] ^= Galois.Multiply(matrixRow[iInput], inputByte);
                    }
                }
            }
        }
    }
}