/**
 * Matrix Algebra over an 8-bit Galois Field
 *
 * Copyright 2015, Backblaze, Inc.
 * Copyright © 2019 Natalia Portillo
 */

using System;
using System.Linq;
using System.Text;

namespace Claunia.ReedSolomon
{
    /// <summary>
    ///     A matrix over the 8-bit Galois field. This class is not performance-critical, so the implementations are
    ///     simple and straightforward.
    /// </summary>
    public class Matrix
    {
        /// <summary>The number of columns in the matrix.</summary>
        readonly int columns;

        /// <summary>
        ///     The data in the matrix, in row major form. To get element (r, c): data[r][c] Because this this is computer
        ///     science, and not math, the indices for both the row and column start at 0.
        /// </summary>
        readonly byte[][] data;

        /// <summary>The number of rows in the matrix.</summary>
        readonly int rows;

        /// <summary>Initialize a matrix of zeros.</summary>
        /// <param name="initRows">The number of rows in the matrix.</param>
        /// <param name="initColumns">The number of columns in the matrix.</param>
        public Matrix(int initRows, int initColumns)
        {
            rows    = initRows;
            columns = initColumns;
            data    = new byte [rows][];

            for(int r = 0; r < rows; r++)
                data[r] = new byte [columns];
        }

        /// <summary>Initializes a matrix with the given row-major data.</summary>
        public Matrix(byte[][] initData)
        {
            rows    = initData.Length;
            columns = initData[0].Length;
            data    = new byte [rows][];

            for(int r = 0; r < rows; r++)
            {
                if(initData[r].Length != columns)
                    throw new ArgumentException("Not all rows have the same number of columns");

                data[r] = new byte[columns];

                for(int c = 0; c < columns; c++)
                    data[r][c] = initData[r][c];
            }
        }

        /// <summary>Returns an identity matrix of the given size.</summary>
        public static Matrix Identity(int size)
        {
            var result = new Matrix(size, size);

            for(int i = 0; i < size; i++)
                result.Set(i, i, 1);

            return result;
        }

        /// <summary>Returns a human-readable string of the matrix contents. Example: [[1, 2], [3, 4]]</summary>
        public override string ToString()
        {
            var result = new StringBuilder();
            result.Append('[');

            for(int r = 0; r < rows; r++)
            {
                if(r != 0)
                    result.Append(", ");

                result.Append('[');

                for(int c = 0; c < columns; c++)
                {
                    if(c != 0)
                        result.Append(", ");

                    result.Append(data[r][c] & 0xFF);
                }

                result.Append(']');
            }

            result.Append(']');

            return result.ToString();
        }

        /// <summary>Returns a human-readable string of the matrix contents. Example: 00 01 02 03 04 05 06 07 08 09 0a 0b</summary>
        public string ToBigString()
        {
            var result = new StringBuilder();

            for(int r = 0; r < rows; r++)
            {
                for(int c = 0; c < columns; c++)
                {
                    int value = Get(r, c);

                    if(value < 0)
                        value += 256;

                    result.Append($"{value:X2} ");
                }

                result.Append("\n");
            }

            return result.ToString();
        }

        /// <summary>Returns the number of columns in this matrix.</summary>
        public int GetColumns() => columns;

        /// <summary>Returns the number of rows in this matrix.</summary>
        public int GetRows() => rows;

        /// <summary>Returns the value at row r, column c.</summary>
        public byte Get(int r, int c)
        {
            if(r    < 0 ||
               rows <= r)
                throw new ArgumentOutOfRangeException(nameof(rows), r, "Row index out of range: " + r);

            if(c       < 0 ||
               columns <= c)
                throw new ArgumentOutOfRangeException(nameof(columns), c, "Column index out of range: " + c);

            return data[r][c];
        }

        /// <summary>Sets the value at row r, column c.</summary>
        public void Set(int r, int c, byte value)
        {
            if(r    < 0 ||
               rows <= r)
                throw new ArgumentOutOfRangeException(nameof(rows), r, "Row index out of range: " + r);

            if(c       < 0 ||
               columns <= c)
                throw new ArgumentOutOfRangeException(nameof(columns), c, "Column index out of range: " + c);

            data[r][c] = value;
        }

        /// <summary>Returns true if this matrix is identical to the other.</summary>
        public override bool Equals(object other)
        {
            if(!(other is Matrix))
                return false;

            for(int r = 0; r < rows; r++)
                if(!data[r].SequenceEqual(((Matrix)other).data[r]))
                    return false;

            return true;
        }

        /// <summary>Multiplies this matrix (the one on the left) by another matrix (the one on the right).</summary>
        public Matrix Times(Matrix right)
        {
            if(GetColumns() != right.GetRows())
                throw new ArgumentException("Columns on left ("                 + GetColumns()    + ") " +
                                            "is different than rows on right (" + right.GetRows() + ")");

            var result = new Matrix(GetRows(), right.GetColumns());

            for(int r = 0; r < GetRows(); r++)
            {
                for(int c = 0; c < right.GetColumns(); c++)
                {
                    byte value = 0;

                    for(int i = 0; i < GetColumns(); i++)
                        value ^= Galois.Multiply(Get(r, i), right.Get(i, c));

                    result.Set(r, c, value);
                }
            }

            return result;
        }

        /// <summary>Returns the concatenation of this matrix and the matrix on the right.</summary>
        public Matrix Augment(Matrix right)
        {
            if(rows != right.rows)
                throw new ArgumentException("Matrices don't have the same number of rows");

            var result = new Matrix(rows, columns + right.columns);

            for(int r = 0; r < rows; r++)
            {
                for(int c = 0; c < columns; c++)
                    result.data[r][c] = data[r][c];

                for(int c = 0; c < right.columns; c++)
                    result.data[r][columns + c] = right.data[r][c];
            }

            return result;
        }

        /// <summary>Returns a part of this matrix.</summary>
        public Matrix Submatrix(int rmin, int cmin, int rmax, int cmax)
        {
            var result = new Matrix(rmax - rmin, cmax - cmin);

            for(int r = rmin; r < rmax; r++)
            {
                for(int c = cmin; c < cmax; c++)
                    result.data[r - rmin][c - cmin] = data[r][c];
            }

            return result;
        }

        /// <summary>Returns one row of the matrix as a byte array.</summary>
        public byte[] GetRow(int row)
        {
            byte[] result = new byte [columns];

            for(int c = 0; c < columns; c++)
                result[c] = Get(row, c);

            return result;
        }

        /// <summary>Exchanges two rows in the matrix.</summary>
        public void SwapRows(int r1, int r2)
        {
            if(r1   < 0   ||
               rows <= r1 ||
               r2   < 0   ||
               rows <= r2)
                throw new ArgumentException("Row index out of range");

            byte[] tmp = data[r1];
            data[r1] = data[r2];
            data[r2] = tmp;
        }

        /// <summary>Returns the inverse of this matrix.</summary>
        /// <returns></returns>
        /// <exception cref="ArgumentException">when the matrix is singular and doesn't have an inverse.</exception>
        public Matrix Invert()
        {
            // Sanity check.
            if(rows != columns)
                throw new ArgumentException("Only square matrices can be inverted");

            // Create a working matrix by augmenting this one with
            // an identity matrix on the right.
            Matrix work = Augment(Identity(rows));

            // Do Gaussian elimination to transform the left half into
            // an identity matrix.
            work.GaussianElimination();

            // The right half is now the inverse.
            return work.Submatrix(0, rows, columns, columns * 2);
        }

        /// <summary>Does the work of matrix inversion. Assumes that this is an r by 2r matrix.</summary>
        void GaussianElimination()
        {
            // Clear out the part below the main diagonal and scale the main
            // diagonal to be 1.
            for(int r = 0; r < rows; r++)
            {
                // If the element on the diagonal is 0, find a row below
                // that has a non-zero and swap them.
                if(data[r][r] == 0)
                    for(int rowBelow = r + 1; rowBelow < rows; rowBelow++)
                        if(data[rowBelow][r] != 0)
                        {
                            SwapRows(r, rowBelow);

                            break;
                        }

                // If we couldn't find one, the matrix is singular.
                if(data[r][r] == 0)
                    throw new ArgumentException("Matrix is singular");

                // Scale to 1.
                if(data[r][r] != 1)
                {
                    byte scale = Galois.Divide(1, data[r][r]);

                    for(int c = 0; c < columns; c++)
                        data[r][c] = Galois.Multiply(data[r][c], scale);
                }

                // Make everything below the 1 be a 0 by subtracting
                // a multiple of it.  (Subtraction and addition are
                // both exclusive or in the Galois field.)
                for(int rowBelow = r + 1; rowBelow < rows; rowBelow++)
                    if(data[rowBelow][r] != 0)
                    {
                        byte scale = data[rowBelow][r];

                        for(int c = 0; c < columns; c++)
                            data[rowBelow][c] ^= Galois.Multiply(scale, data[r][c]);
                    }
            }

            // Now clear the part above the main diagonal.
            for(int d = 0; d < rows; d++)
            {
                for(int rowAbove = 0; rowAbove < d; rowAbove++)
                    if(data[rowAbove][d] != 0)
                    {
                        byte scale = data[rowAbove][d];

                        for(int c = 0; c < columns; c++)
                            data[rowAbove][c] ^= Galois.Multiply(scale, data[d][c]);
                    }
            }
        }
    }
}