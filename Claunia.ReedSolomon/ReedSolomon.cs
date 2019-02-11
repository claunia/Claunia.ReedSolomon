/**
 * Reed-Solomon Coding over 8-bit values.
 *
 * Copyright 2015, Backblaze, Inc.
 * Copyright © 2019 Natalia Portillo
 */

using System;

namespace Claunia.ReedSolomon
{
    /// <summary>Reed-Solomon Coding over 8-bit values.</summary>
    public class ReedSolomon
    {
        readonly ICodingLoop codingLoop;

        readonly int    dataShardCount;
        readonly Matrix matrix;

        // Rows from the matrix for encoding parity, each one as its own
        // byte array to allow for efficient access while encoding.
        readonly byte[][] parityRows;
        readonly int      parityShardCount;
        readonly int      totalShardCount;

        /// <summary>Initializes a new encoder/decoder, with a chosen coding loop.</summary>
        public ReedSolomon(int dataShardCount, int parityShardCount, ICodingLoop codingLoop)
        {
            // We can have at most 256 shards total, as any more would
            // lead to duplicate rows in the Vandermonde matrix, which
            // would then lead to duplicate rows in the built matrix
            // below. Then any subset of the rows containing the duplicate
            // rows would be singular.
            if(256 < dataShardCount + parityShardCount)
                throw new ArgumentException("too many shards - max is 256");

            this.dataShardCount   = dataShardCount;
            this.parityShardCount = parityShardCount;
            this.codingLoop       = codingLoop;
            totalShardCount       = dataShardCount + parityShardCount;
            matrix                = BuildMatrix(dataShardCount, totalShardCount);
            parityRows            = new byte [parityShardCount][];

            for(int i = 0; i < parityShardCount; i++)
                parityRows[i] = matrix.GetRow(dataShardCount + i);
        }

        /// <summary>Creates a ReedSolomon codec with the default coding loop.</summary>
        public static ReedSolomon Create(int dataShardCount, int parityShardCount) =>
            new ReedSolomon(dataShardCount, parityShardCount, new InputOutputByteTableCodingLoop());

        /// <summary>Returns the number of data shards.</summary>
        public int GetDataShardCount() => dataShardCount;

        /// <summary>Returns the number of parity shards.</summary>
        public int GetParityShardCount() => parityShardCount;

        /// <summary>Returns the total number of shards.</summary>
        /// <returns></returns>
        public int GetTotalShardCount() => totalShardCount;

        /// <summary>Encodes parity for a set of data shards.</summary>
        /// <param name="shards">
        ///     An array containing data shards followed by parity shards. Each shard is a byte array, and they
        ///     must all be the same size.
        /// </param>
        /// <param name="offset">The index of the first byte in each shard to encode.</param>
        /// <param name="byteCount">The number of bytes to encode in each shard.</param>
        public void EncodeParity(byte[][] shards, int offset, int byteCount)
        {
            // Check arguments.
            CheckBuffersAndSizes(shards, offset, byteCount);

            // Build the array of output buffers.
            byte[][] outputs = new byte [parityShardCount][];
            Array.Copy(shards, dataShardCount, outputs, 0, parityShardCount);

            // Do the coding.
            codingLoop.CodeSomeShards(parityRows, shards, dataShardCount, outputs, parityShardCount, offset, byteCount);
        }

        /// <summary>Returns true if the parity shards contain the right data.</summary>
        /// <param name="shards">
        ///     An array containing data shards followed by parity shards. Each shard is a byte array, and they
        ///     must all be the same size.
        /// </param>
        /// <param name="firstByte">The index of the first byte in each shard to check.</param>
        /// <param name="byteCount">The number of bytes to check in each shard.</param>
        /// <returns></returns>
        public bool IsParityCorrect(byte[][] shards, int firstByte, int byteCount)
        {
            // Check arguments.
            CheckBuffersAndSizes(shards, firstByte, byteCount);

            // Build the array of buffers being checked.
            byte[][] toCheck = new byte [parityShardCount][];
            Array.Copy(shards, dataShardCount, toCheck, 0, parityShardCount);

            // Do the checking.
            return codingLoop.CheckSomeShards(parityRows, shards, dataShardCount, toCheck, parityShardCount, firstByte,
                                              byteCount, null);
        }

        /// <summary>
        ///     Returns true if the parity shards contain the right data. This method may be significantly faster than the one
        ///     above that does not use a temporary buffer.
        /// </summary>
        /// <param name="shards">
        ///     An array containing data shards followed by parity shards. Each shard is a byte array, and they
        ///     must all be the same size.
        /// </param>
        /// <param name="firstByte">The index of the first byte in each shard to check.</param>
        /// <param name="byteCount">The number of bytes to check in each shard.</param>
        /// <param name="tempBuffer">A temporary buffer (the same size as each of the shards) to use when computing parity.</param>
        public bool IsParityCorrect(byte[][] shards, int firstByte, int byteCount, byte[] tempBuffer)
        {
            // Check arguments.
            CheckBuffersAndSizes(shards, firstByte, byteCount);

            if(tempBuffer.Length < firstByte + byteCount)
                throw new ArgumentException("tempBuffer is not big enough");

            // Build the array of buffers being checked.
            byte[][] toCheck = new byte [parityShardCount][];
            Array.Copy(shards, dataShardCount, toCheck, 0, parityShardCount);

            // Do the checking.
            return codingLoop.CheckSomeShards(parityRows, shards, dataShardCount, toCheck, parityShardCount, firstByte,
                                              byteCount, tempBuffer);
        }

        /// <summary>
        ///     Given a list of shards, some of which contain data, fills in the ones that don't have data. Quickly does
        ///     nothing if all of the shards are present. If any shards are missing (based on the flags in shardsPresent), the data
        ///     in those shards is recomputed and filled in.
        /// </summary>
        public void DecodeMissing(byte[][] shards, bool[] shardPresent, int offset, int byteCount)
        {
            // Check arguments.
            CheckBuffersAndSizes(shards, offset, byteCount);

            // Quick check: are all of the shards present?  If so, there's
            // nothing to do.
            int numberPresent = 0;

            for(int i = 0; i < totalShardCount; i++)
                if(shardPresent[i])
                    numberPresent += 1;

            if(numberPresent == totalShardCount)
                return;

            // More complete sanity check
            if(numberPresent < dataShardCount)
                throw new ArgumentException("Not enough shards present");

            // Pull out the rows of the matrix that correspond to the
            // shards that we have and build a square matrix.  This
            // matrix could be used to generate the shards that we have
            // from the original data.
            //
            // Also, pull out an array holding just the shards that
            // correspond to the rows of the submatrix.  These shards
            // will be the input to the decoding process that re-creates
            // the missing data shards.
            var      subMatrix = new Matrix(dataShardCount, dataShardCount);
            byte[][] subShards = new byte [dataShardCount][];

            {
                int subMatrixRow = 0;

                for(int matrixRow = 0; matrixRow < totalShardCount && subMatrixRow < dataShardCount; matrixRow++)
                    if(shardPresent[matrixRow])
                    {
                        for(int c = 0; c < dataShardCount; c++)
                            subMatrix.Set(subMatrixRow, c, matrix.Get(matrixRow, c));

                        subShards[subMatrixRow] =  shards[matrixRow];
                        subMatrixRow            += 1;
                    }
            }

            // Invert the matrix, so we can go from the encoded shards
            // back to the original data.  Then pull out the row that
            // generates the shard that we want to decode.  Note that
            // since this matrix maps back to the orginal data, it can
            // be used to create a data shard, but not a parity shard.
            Matrix dataDecodeMatrix = subMatrix.Invert();

            // Re-create any data shards that were missing.
            //
            // The input to the coding is all of the shards we actually
            // have, and the output is the missing data shards.  The computation
            // is done using the special decode matrix we just built.
            byte[][] outputs     = new byte [parityShardCount][];
            byte[][] matrixRows  = new byte [parityShardCount][];
            int      outputCount = 0;

            for(int iShard = 0; iShard < dataShardCount; iShard++)
                if(!shardPresent[iShard])
                {
                    outputs[outputCount]    =  shards[iShard];
                    matrixRows[outputCount] =  dataDecodeMatrix.GetRow(iShard);
                    outputCount             += 1;
                }

            codingLoop.CodeSomeShards(matrixRows, subShards, dataShardCount, outputs, outputCount, offset, byteCount);

            // Now that we have all of the data shards intact, we can
            // compute any of the parity that is missing.
            //
            // The input to the coding is ALL of the data shards, including
            // any that we just calculated.  The output is whichever of the
            // data shards were missing.
            outputCount = 0;

            for(int iShard = dataShardCount; iShard < totalShardCount; iShard++)
                if(!shardPresent[iShard])
                {
                    outputs[outputCount]    =  shards[iShard];
                    matrixRows[outputCount] =  parityRows[iShard - dataShardCount];
                    outputCount             += 1;
                }

            codingLoop.CodeSomeShards(matrixRows, shards, dataShardCount, outputs, outputCount, offset, byteCount);
        }

        /// <summary>Checks the consistency of arguments passed to public methods.</summary>
        void CheckBuffersAndSizes(byte[][] shards, int offset, int byteCount)
        {
            // The number of buffers should be equal to the number of
            // data shards plus the number of parity shards.
            if(shards.Length != totalShardCount)
                throw new ArgumentException("wrong number of shards: " + shards.Length);

            // All of the shard buffers should be the same length.
            int shardLength = shards[0].Length;

            for(int i = 1; i < shards.Length; i++)
                if(shards[i].Length != shardLength)
                    throw new ArgumentException("Shards are different sizes");

            // The offset and byteCount must be non-negative and fit in the buffers.
            if(offset < 0)
                throw new ArgumentException("offset is negative: " + offset);

            if(byteCount < 0)
                throw new ArgumentException("byteCount is negative: " + byteCount);

            if(shardLength < offset + byteCount)
                throw new ArgumentException("buffers to small: " + byteCount + offset);
        }

        /// <summary>
        ///     Create the matrix to use for encoding, given the number of data shards and the number of total shards. The top
        ///     square of the matrix is guaranteed to be an identity matrix, which means that the data shards are unchanged after
        ///     encoding.
        /// </summary>
        static Matrix BuildMatrix(int dataShards, int totalShards)
        {
            // Start with a Vandermonde matrix.  This matrix would work,
            // in theory, but doesn't have the property that the data
            // shards are unchanged after encoding.
            Matrix vandermonde = Vandermonde(totalShards, dataShards);

            // Multiple by the inverse of the top square of the matrix.
            // This will make the top square be the identity matrix, but
            // preserve the property that any square subset of rows is
            // invertible.
            Matrix top = vandermonde.Submatrix(0, 0, dataShards, dataShards);

            return vandermonde.Times(top.Invert());
        }

        /// <summary>
        ///     Create a Vandermonde matrix, which is guaranteed to have the property that any subset of rows that forms a
        ///     square matrix is invertible.
        /// </summary>
        /// <param name="rows">Number of rows in the result.</param>
        /// <param name="cols">Number of columns in the result.</param>
        /// <returns>A Matrix.</returns>
        static Matrix Vandermonde(int rows, int cols)
        {
            var result = new Matrix(rows, cols);

            for(int r = 0; r < rows; r++)
            {
                for(int c = 0; c < cols; c++)
                    result.Set(r, c, Galois.Exp((byte)r, c));
            }

            return result;
        }
    }
}