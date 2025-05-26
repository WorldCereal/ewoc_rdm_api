using System;

namespace IIASA.WorldCereal.Rdm.Core.Vector
{
    public static class VectorFileReaderExtension
    {
        public static int GetOrdinalExt(this VectorFileReader inst, string colName)
        {
            if (string.IsNullOrEmpty(colName))
            {
                return -1;
            }

            try
            {
                return inst.GetOrdinal(colName);
            }
            catch (IndexOutOfRangeException)
            {
                return -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static string GetValueWithType(this VectorFileReader inst, int colIndex, Type type)
        {
            if (type == typeof(int))
            {
                return GetInt32Ext(inst, colIndex).ToString();
            }

            if (type == typeof(long))
            {
                return GetInt64Ext(inst, colIndex).ToString();
            }

            if (type == typeof(string))
            {
                return GetStringExt(inst, colIndex);
            }

            return string.Empty;
        }

        public static int GetInt32Ext(this VectorFileReader inst, int colIndex)
        {
            if (colIndex == -1)
            {
                return default;
            }

            return inst.GetInt32(colIndex);
        }

        public static long GetInt64Ext(this VectorFileReader inst, int colIndex)
        {
            if (colIndex == -1)
            {
                return default;
            }

            return inst.GetInt64(colIndex);
        }

        public static double GetDoubleExt(this VectorFileReader inst, int colIndex)
        {
            if (colIndex == -1)
            {
                return default;
            }

            return inst.GetDouble(colIndex);
        }

        public static string GetStringExt(this VectorFileReader inst, int colIndex)
        {
            if (colIndex == -1)
            {
                return string.Empty;
            }

            return inst.GetString(colIndex);
        }

        public static DateTime GetDateTimeExt(this VectorFileReader inst, int colIndex)
        {
            if (colIndex == -1)
            {
                return default;
            }

            return inst.GetDateTime(colIndex);
        }
    }
}