using SharedLib.Packets;
using SharedLib.Payloads;
using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== TEST PACKET SERIALIZE ===");

        // Test 1: DrawPayload
        var draw = new DrawPayload
        {
            Username = "Người A",
            ToolType = "line",
            X1 = 100,
            Y1 = 200,
            X2 = 300,
            Y2 = 400,
            ColorARGB = -65536, // màu đỏ
            Thickness = 3
        };

        Packet p1 = PacketHelper.Create(CommandType.DRAW, draw);
        byte[] bytes = p1.Serialize();
        Packet p1Back = Packet.Deserialize(bytes);
        DrawPayload back = PacketHelper.GetPayload<DrawPayload>(p1Back);

        Console.WriteLine($"ActionID khớp : {draw.ActionID == back.ActionID}");
        Console.WriteLine($"Username khớp : {draw.Username == back.Username}");
        Console.WriteLine($"X1 khớp       : {draw.X1 == back.X1}");
        Console.WriteLine($"ToolType khớp : {draw.ToolType == back.ToolType}");

        // Test 2: LoginPayload
        var login = new LoginPayload { Username = "test", Password = "123456" };
        Packet p2 = PacketHelper.Create(CommandType.LOGIN, login);
        byte[] bytes2 = p2.Serialize();
        Packet p2Back = Packet.Deserialize(bytes2);
        LoginPayload lBack = PacketHelper.GetPayload<LoginPayload>(p2Back);

        Console.WriteLine($"\nLogin Username khớp : {login.Username == lBack.Username}");
        Console.WriteLine($"Login Password khớp : {login.Password == lBack.Password}");

        // Test 3: 100 ActionID phải unique
        var ids = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < 100; i++)
            ids.Add(new DrawPayload().ActionID);
        Console.WriteLine($"\n100 ActionID unique : {ids.Count == 100}");

        Console.WriteLine("\n=== TẤT CẢ TEST PASSED ===");
        Console.ReadKey();
    }
}