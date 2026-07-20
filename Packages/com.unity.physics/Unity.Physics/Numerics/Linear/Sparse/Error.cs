namespace Unity.Numerics.Linear.Sparse.Primitives
{
    internal struct Assertions
    {
        public static void AssertCompatibleDimensions(int n, int m)
        {
            UnityEngine.Debug.Assert(n == m, "Vectors must have the same size");
        }

        public static void AssertCompatibleDimensions(Vector a, Vector b)
        {
            AssertCompatibleDimensions(a.Dimension, b.Dimension);
        }

        public static void AssertCompatibleDimensions(Matrix m, Vector a)
        {
            AssertCompatibleDimensions(m.NumCols, a.Dimension);
        }

        public static void AssertCompatibleDimensionsTranspose(Matrix m, Vector a)
        {
            AssertCompatibleDimensions(m.NumRows, a.Dimension);
        }

        public static void AssertCompatibleDimensions(Matrix m, Dense.Primitives.Vector a)
        {
            AssertCompatibleDimensions(m.NumCols, a.Dimension);
        }

        public static void AssertCompatibleDimensionsTranspose(Matrix m, Dense.Primitives.Vector a)
        {
            AssertCompatibleDimensions(m.NumRows, a.Dimension);
        }
    }
}
