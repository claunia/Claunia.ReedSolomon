/**
 * One specific ordering/nesting of the coding loops.
 *
 * Copyright 2015, Backblaze, Inc.  All rights reserved.
 * Copyright © 2019 Natalia Portillo
 */

namespace Claunia.ReedSolomon
{
    public class OutputInputByteExpCodingLoop : CodingLoopBase
    {
        public override void CodeSomeShards(byte[][] matrixRows, byte[][] inputs, int inputCount, byte[][] outputs,
                                            int outputCount, int offset, int byteCount)
        {
            for(int iOutput = 0; iOutput < outputCount; iOutput++)
            {
                byte[] outputShard = outputs[iOutput];
                byte[] matrixRow   = matrixRows[iOutput];

                {
                    int    iInput     = 0;
                    byte[] inputShard = inputs[iInput];
                    byte   matrixByte = matrixRow[iInput];

                    for(int iByte = offset; iByte < offset + byteCount; iByte++)
                        outputShard[iByte] = Galois.Multiply(matrixByte, inputShard[iByte]);
                }

                for(int iInput = 1; iInput < inputCount; iInput++)
                {
                    byte[] inputShard = inputs[iInput];
                    byte   matrixByte = matrixRow[iInput];

                    for(int iByte = offset; iByte < offset + byteCount; iByte++)
                        outputShard[iByte] ^= Galois.Multiply(matrixByte, inputShard[iByte]);
                }
            }
        }
    }
}