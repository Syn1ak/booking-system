using System.Buffers.Binary;

namespace BookingSystem.Api.Common;

public static class RowVersion
{
    /// <summary>
    /// A rowversion as a number a client can compare. Big-endian, which is the order SQL Server
    /// increments the eight bytes in, so the comparison agrees with the database's own ordering.
    /// </summary>
    public static long ToSequence(byte[] version) => BinaryPrimitives.ReadInt64BigEndian(version);
}
