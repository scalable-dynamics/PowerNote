namespace PowerNote.Models;
internal static class PowerFxSample
{
    public static string Code = @"
Set( Radius, 10 )

Pi = 3.14159265359

Area = Pi * Radius * Radius

Set( Radius, 300 )

Circumference = 2 * Pi * Radius

Set( Radius, 40 )

Area

Circumference

";
}
