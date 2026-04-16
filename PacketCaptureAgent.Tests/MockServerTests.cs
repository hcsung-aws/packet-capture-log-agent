using System.Text.Json;

namespace PacketCaptureAgent.Tests;

public class MockServerTests
{
    private static ProtocolDefinition CreateProtocol()
    {
        return new ProtocolDefinition
        {
            Protocol = new ProtocolInfo
            {
                Name = "Test",
                Endian = "little",
                Header = new HeaderInfo
                {
                    Size = 4,
                    SizeField = "length",
                    TypeField = "type",
                    Fields = new List<HeaderFieldInfo>
                    {
                        new() { Name = "length", Type = "uint16", Offset = 0 },
                        new() { Name = "type", Type = "uint16", Offset = 2 },
                    }
                }
            },
            Packets = new List<PacketDefinition>
            {
                new() { Type = 0x0101, Name = "CS_LOGIN", Fields = new()
                {
                    new() { Name = "accountId", Type = "string", Length = 32 },
                    new() { Name = "password", Type = "string", Length = 32 },
                }},
                new() { Type = 0x0102, Name = "SC_LOGIN_RESULT", Fields = new()
                {
                    new() { Name = "success", Type = "uint8" },
                    new() { Name = "accountUid", Type = "uint64" },
                    new() { Name = "message", Type = "string", Length = 64 },
                }},
                new() { Type = 0x0202, Name = "SC_CHAR_LIST", Fields = new()
                {
                    new() { Name = "count", Type = "uint8" },
                }},
                new() { Type = 0x0203, Name = "CS_CHAR_CREATE", Fields = new()
                {
                    new() { Name = "name", Type = "string", Length = 20 },
                    new() { Name = "charType", Type = "uint8" },
                }},
                new() { Type = 0x0204, Name = "SC_CHAR_CREATE_RESULT", Fields = new()
                {
                    new() { Name = "success", Type = "uint8" },
                    new() { Name = "charUid", Type = "uint64" },
                    new() { Name = "message", Type = "string", Length = 32 },
                }},
                new() { Type = 0x0205, Name = "CS_CHAR_SELECT", Fields = new()
                {
                    new() { Name = "charUid", Type = "uint64" },
                }},
                new() { Type = 0x0206, Name = "SC_CHAR_INFO", Fields = new()
                {
                    new() { Name = "charUid", Type = "uint64" },
                    new() { Name = "name", Type = "string", Length = 32 },
                    new() { Name = "level", Type = "uint16" },
                    new() { Name = "exp", Type = "uint32" },
                    new() { Name = "posX", Type = "int16" },
                    new() { Name = "posY", Type = "int16" },
                    new() { Name = "hp", Type = "uint16" },
                    new() { Name = "maxHp", Type = "uint16" },
                    new() { Name = "gold", Type = "uint64" },
                }},
                new() { Type = 0x0301, Name = "SC_ATTENDANCE_INFO", Fields = new()
                {
                    new() { Name = "todayAttended", Type = "uint8" },
                    new() { Name = "rewardGold", Type = "uint32" },
                }},
                new() { Type = 0x0401, Name = "CS_MOVE", Fields = new()
                {
                    new() { Name = "dirX", Type = "int8" },
                    new() { Name = "dirY", Type = "int8" },
                }},
                new() { Type = 0x0402, Name = "SC_MOVE_RESULT", Fields = new()
                {
                    new() { Name = "success", Type = "uint8" },
                    new() { Name = "posX", Type = "int16" },
                    new() { Name = "posY", Type = "int16" },
                }},
                new() { Type = 0x0501, Name = "CS_ATTACK", Fields = new()
                {
                    new() { Name = "targetUid", Type = "uint64" },
                }},
                new() { Type = 0x0502, Name = "SC_ATTACK_RESULT", Fields = new()
                {
                    new() { Name = "success", Type = "uint8" },
                    new() { Name = "targetUid", Type = "uint64" },
                    new() { Name = "damage", Type = "uint16" },
                    new() { Name = "targetHp", Type = "uint16" },
                }},
                new() { Type = 0x0503, Name = "SC_NPC_SPAWN", Fields = new()
                {
                    new() { Name = "npcUid", Type = "uint64" },
                    new() { Name = "posX", Type = "int16" },
                    new() { Name = "posY", Type = "int16" },
                    new() { Name = "hp", Type = "uint16" },
                    new() { Name = "maxHp", Type = "uint16" },
                    new() { Name = "npcType", Type = "uint8" },
                }},
                new() { Type = 0x0504, Name = "SC_NPC_DEATH", Fields = new()
                {
                    new() { Name = "npcUid", Type = "uint64" },
                    new() { Name = "expReward", Type = "uint32" },
                    new() { Name = "goldReward", Type = "uint32" },
                }},
                new() { Type = 0x0601, Name = "SC_ITEM_DROP", Fields = new()
                {
                    new() { Name = "slot", Type = "uint8" },
                    new() { Name = "itemId", Type = "uint16" },
                    new() { Name = "itemName", Type = "string", Length = 32 },
                }},
                new() { Type = 0x0701, Name = "SC_INVENTORY_LIST", Fields = new()
                {
                    new() { Name = "count", Type = "uint8" },
                }},
                new() { Type = 0x0801, Name = "CS_SHOP_OPEN", Fields = new() },
                new() { Type = 0x0802, Name = "SC_SHOP_LIST", Fields = new()
                {
                    new() { Name = "count", Type = "uint8" },
                }},
                new() { Type = 0x0803, Name = "CS_SHOP_BUY", Fields = new()
                {
                    new() { Name = "itemId", Type = "uint16" },
                }},
                new() { Type = 0x0804, Name = "SC_SHOP_RESULT", Fields = new()
                {
                    new() { Name = "success", Type = "uint8" },
                    new() { Name = "itemId", Type = "uint16" },
                    new() { Name = "remainGold", Type = "uint64" },
                    new() { Name = "message", Type = "string", Length = 32 },
                }},
                new() { Type = 0x0805, Name = "CS_SHOP_SELL", Fields = new()
                {
                    new() { Name = "slot", Type = "uint8" },
                }},
                new() { Type = 0x0901, Name = "SC_INVENTORY_UPDATE", Fields = new()
                {
                    new() { Name = "slot", Type = "uint8" },
                    new() { Name = "itemId", Type = "uint16" },
                    new() { Name = "itemName", Type = "string", Length = 32 },
                }},
                new() { Type = 0x0A01, Name = "CS_ITEM_USE", Fields = new()
                {
                    new() { Name = "slot", Type = "uint8" },
                }},
                new() { Type = 0x0A02, Name = "SC_ITEM_USE_RESULT", Fields = new()
                {
                    new() { Name = "charUid", Type = "uint64" },
                    new() { Name = "charName", Type = "string", Length = 20 },
                    new() { Name = "itemName", Type = "string", Length = 32 },
                    new() { Name = "effectType", Type = "uint8" },
                    new() { Name = "effectValue", Type = "uint16" },
                }},
                new() { Type = 0x0B01, Name = "CS_ITEM_EQUIP", Fields = new()
                {
                    new() { Name = "slot", Type = "uint8" },
                }},
                new() { Type = 0x0B02, Name = "SC_EQUIP_RESULT", Fields = new()
                {
                    new() { Name = "success", Type = "uint8" },
                    new() { Name = "slot", Type = "uint8" },
                    new() { Name = "atk", Type = "uint16" },
                    new() { Name = "def", Type = "uint16" },
                    new() { Name = "weaponName", Type = "string", Length = 32 },
                    new() { Name = "armorName", Type = "string", Length = 32 },
                    new() { Name = "message", Type = "string", Length = 32 },
                }},
                new() { Type = 0xFF01, Name = "CS_HEARTBEAT", Fields = new() },
                new() { Type = 0xFF02, Name = "SC_HEARTBEAT", Fields = new() },
                // 怨좎젙 ?묐떟 ?뚯뒪?몄슜
                new() { Type = 0xC01, Name = "CS_QUEST_LIST", Fields = new()
                {
                    new() { Name = "npcUid", Type = "uint64" },
                }},
                new() { Type = 0xC02, Name = "SC_QUEST_LIST", Fields = new()
                {
                    new() { Name = "count", Type = "uint8" },
                }},
            }
        };
    }

    private static MockRuleSet CreateRuleSet()
    {
        return new MockRuleSet
        {
            Protocol = "Test",
            Rules = new()
            {
                new() { Trigger = "CS_LOGIN", Stateful = true, Responses = new() { new() { Packet = "SC_LOGIN_RESULT" }, new() { Packet = "SC_CHAR_LIST" } } },
                new() { Trigger = "CS_CHAR_CREATE", Stateful = true, Responses = new() { new() { Packet = "SC_CHAR_CREATE_RESULT" }, new() { Packet = "SC_CHAR_LIST" } } },
                new() { Trigger = "CS_CHAR_SELECT", Stateful = true, Responses = new() { new() { Packet = "SC_CHAR_INFO" }, new() { Packet = "SC_NPC_SPAWN" }, new() { Packet = "SC_ATTENDANCE_INFO" }, new() { Packet = "SC_INVENTORY_LIST" } } },
                new() { Trigger = "CS_MOVE", Stateful = true, Responses = new() { new() { Packet = "SC_MOVE_RESULT" } } },
                new() { Trigger = "CS_ATTACK", Stateful = true, Responses = new() { new() { Packet = "SC_ATTACK_RESULT" } } },
                new() { Trigger = "CS_SHOP_OPEN", Stateful = true, Responses = new() { new() { Packet = "SC_SHOP_LIST" } } },
                new() { Trigger = "CS_SHOP_BUY", Stateful = true, Responses = new() { new() { Packet = "SC_SHOP_RESULT" } } },
                new() { Trigger = "CS_SHOP_SELL", Stateful = true, Responses = new() { new() { Packet = "SC_SHOP_RESULT" } } },
                new() { Trigger = "CS_ITEM_USE", Stateful = true, Responses = new() { new() { Packet = "SC_ITEM_USE_RESULT" } } },
                new() { Trigger = "CS_ITEM_EQUIP", Stateful = true, Responses = new() { new() { Packet = "SC_EQUIP_RESULT" } } },
                new() { Trigger = "CS_HEARTBEAT", Stateful = true, Responses = new() { new() { Packet = "SC_HEARTBEAT" } } },
                new() { Trigger = "CS_QUEST_LIST", Stateful = false, Responses = new()
                {
                    new() { Packet = "SC_QUEST_LIST", Fields = new() { ["count"] = JsonSerializer.SerializeToElement(0) } }
                }},
            }
        };
    }

    private static MockServer CreateServer()
    {
        var protocol = CreateProtocol();
        var ruleSet = CreateRuleSet();
        return new MockServer(protocol, ruleSet, TextWriter.Null);
    }

    private static ParsedPacket MakeRequest(string name, Dictionary<string, object>? fields = null)
    {
        var pkt = new ParsedPacket { Name = name };
        if (fields != null)
            foreach (var (k, v) in fields)
                pkt.Fields[k] = v;
        return pkt;
    }

    // ?? Login ??

    [Fact]
    public void Login_ReturnsLoginResultAndCharList()
    {
        var server = CreateServer();
        var session = new MockSession();
        var resp = server.GenerateResponses(MakeRequest("CS_LOGIN", new()
        {
            ["accountId"] = "tester", ["password"] = ""
        }), session);

        Assert.Equal(2, resp.Count);
        Assert.Equal("SC_LOGIN_RESULT", resp[0].Name);
        Assert.Equal("SC_CHAR_LIST", resp[1].Name);
        Assert.True(session.LoggedIn);
        Assert.NotEqual(0UL, session.AccountUid);
    }

    // ?? Char Create ??

    [Fact]
    public void CharCreate_AddsCharacterToSession()
    {
        var server = CreateServer();
        var session = new MockSession();
        var resp = server.GenerateResponses(MakeRequest("CS_CHAR_CREATE", new()
        {
            ["name"] = "hero", ["charType"] = (byte)1
        }), session);

        Assert.Equal(2, resp.Count);
        Assert.Equal("SC_CHAR_CREATE_RESULT", resp[0].Name);
        Assert.Equal("SC_CHAR_LIST", resp[1].Name);
        Assert.Single(session.Characters);
        Assert.Equal("hero", session.Characters[0].Name);
    }

    // ?? Char Select ??

    [Fact]
    public void CharSelect_SetsInGameAndSpawnsNpcs()
    {
        var server = CreateServer();
        var session = new MockSession();
        session.Characters.Add(new CharListEntry { CharUid = 42, Name = "hero", CharType = 1, Level = 1 });

        var resp = server.GenerateResponses(MakeRequest("CS_CHAR_SELECT", new()
        {
            ["charUid"] = 42UL
        }), session);

        Assert.True(session.InGame);
        Assert.Equal(42UL, session.CharUid);
        Assert.Equal("hero", session.CharName);
        Assert.True(session.Npcs.Count > 0);
        // SC_CHAR_INFO + N 횞 SC_NPC_SPAWN + SC_ATTENDANCE_INFO + SC_INVENTORY_LIST
        Assert.True(resp.Count >= 4);
        Assert.Equal("SC_CHAR_INFO", resp[0].Name);
    }

    // ?? Move ??

    [Fact]
    public void Move_UpdatesPosition()
    {
        var server = CreateServer();
        var session = new MockSession { PosX = 10, PosY = 10 };

        server.GenerateResponses(MakeRequest("CS_MOVE", new()
        {
            ["dirX"] = (sbyte)1, ["dirY"] = (sbyte)-1
        }), session);

        Assert.Equal(11, session.PosX);
        Assert.Equal(9, session.PosY);
    }

    [Fact]
    public void Move_ClampsToMapBounds()
    {
        var server = CreateServer();
        var session = new MockSession { PosX = 0, PosY = 19 };

        server.GenerateResponses(MakeRequest("CS_MOVE", new()
        {
            ["dirX"] = (sbyte)-1, ["dirY"] = (sbyte)1
        }), session);

        Assert.Equal(0, session.PosX);
        Assert.Equal(19, session.PosY);
    }

    // ?? Attack ??

    [Fact]
    public void Attack_ReducesNpcHp()
    {
        var server = CreateServer();
        var session = new MockSession();
        session.Npcs[1] = new NpcState { Uid = 1, Hp = 50, MaxHp = 50 };

        var resp = server.GenerateResponses(MakeRequest("CS_ATTACK", new()
        {
            ["targetUid"] = 1UL
        }), session);

        Assert.Equal("SC_ATTACK_RESULT", resp[0].Name);
        Assert.Equal((ushort)40, session.Npcs[1].Hp);
    }

    [Fact]
    public void Attack_KillNpc_DeathAndRespawn()
    {
        var server = CreateServer();
        var session = new MockSession();
        session.Npcs[1] = new NpcState { Uid = 1, Hp = 5, MaxHp = 50, NpcType = 0 };

        var resp = server.GenerateResponses(MakeRequest("CS_ATTACK", new()
        {
            ["targetUid"] = 1UL
        }), session);

        // SC_ATTACK_RESULT + SC_NPC_DEATH + (maybe SC_ITEM_DROP) + SC_NPC_SPAWN
        Assert.Contains(resp, r => r.Name == "SC_ATTACK_RESULT");
        Assert.Contains(resp, r => r.Name == "SC_NPC_DEATH");
        Assert.Contains(resp, r => r.Name == "SC_NPC_SPAWN");
        Assert.DoesNotContain(session.Npcs, kv => kv.Key == 1); // old npc removed
        Assert.Single(session.Npcs); // new npc spawned
    }

    [Fact]
    public void Attack_InvalidTarget_Fails()
    {
        var server = CreateServer();
        var session = new MockSession();

        var resp = server.GenerateResponses(MakeRequest("CS_ATTACK", new()
        {
            ["targetUid"] = 999UL
        }), session);

        Assert.Single(resp);
        Assert.Equal("SC_ATTACK_RESULT", resp[0].Name);
    }

    // ?? Shop ??

    [Fact]
    public void ShopBuy_DeductsGold()
    {
        var server = CreateServer();
        var session = new MockSession { Gold = 500 };

        server.GenerateResponses(MakeRequest("CS_SHOP_BUY", new()
        {
            ["itemId"] = (ushort)3
        }), session);

        Assert.Equal(200UL, session.Gold); // 500 - 3*100
        Assert.Single(session.Inventory);
    }

    [Fact]
    public void ShopBuy_NotEnoughGold_Fails()
    {
        var server = CreateServer();
        var session = new MockSession { Gold = 10 };

        server.GenerateResponses(MakeRequest("CS_SHOP_BUY", new()
        {
            ["itemId"] = (ushort)1
        }), session);

        Assert.Equal(10UL, session.Gold);
        Assert.Empty(session.Inventory);
    }

    [Fact]
    public void ShopSell_AddsGold()
    {
        var server = CreateServer();
        var session = new MockSession { Gold = 100 };
        session.Inventory.Add(new InventorySlot { Slot = 0, ItemId = 1, ItemName = "Sword" });

        server.GenerateResponses(MakeRequest("CS_SHOP_SELL", new()
        {
            ["slot"] = (byte)0
        }), session);

        Assert.Equal(150UL, session.Gold);
        Assert.Empty(session.Inventory);
    }

    // ?? Heartbeat ??

    [Fact]
    public void Heartbeat_ReturnsHeartbeat()
    {
        var server = CreateServer();
        var session = new MockSession();

        var resp = server.GenerateResponses(MakeRequest("CS_HEARTBEAT"), session);

        Assert.Single(resp);
        Assert.Equal("SC_HEARTBEAT", resp[0].Name);
    }

    // ?? 怨좎젙 ?묐떟 (鍮꾩긽?? ??

    [Fact]
    public void NonStateful_ReturnsRuleDefaults()
    {
        var server = CreateServer();
        var session = new MockSession();

        var resp = server.GenerateResponses(MakeRequest("CS_QUEST_LIST", new()
        {
            ["npcUid"] = 0UL
        }), session);

        Assert.Single(resp);
        Assert.Equal("SC_QUEST_LIST", resp[0].Name);
    }

    // ?? ?몄뀡 ?낅┰????

    [Fact]
    public void Sessions_AreIndependent()
    {
        var server = CreateServer();
        var s1 = new MockSession();
        var s2 = new MockSession();

        server.GenerateResponses(MakeRequest("CS_LOGIN", new()
        {
            ["accountId"] = "user1", ["password"] = ""
        }), s1);
        server.GenerateResponses(MakeRequest("CS_LOGIN", new()
        {
            ["accountId"] = "user2", ["password"] = ""
        }), s2);

        Assert.NotEqual(s1.AccountUid, s2.AccountUid);
    }

    // ?? MockRuleBuilder ??

    [Fact]
    public void MockRuleBuilder_BuildsFromCatalog()
    {
        var protocol = CreateProtocol();
        var catalog = new ActionCatalog
        {
            Protocol = "Test",
            Actions = new()
            {
                new CatalogAction
                {
                    Id = "login",
                    Packets = new()
                    {
                        new() { Direction = "SEND", Name = "CS_LOGIN" },
                        new() { Direction = "RECV", Name = "SC_LOGIN_RESULT" },
                        new() { Direction = "RECV", Name = "SC_CHAR_LIST" },
                    }
                },
                new CatalogAction
                {
                    Id = "move",
                    Packets = new()
                    {
                        new() { Direction = "SEND", Name = "CS_MOVE" },
                        new() { Direction = "RECV", Name = "SC_MOVE_RESULT" },
                    }
                }
            }
        };

        var builder = new MockRuleBuilder();
        var ruleSet = builder.Build(catalog, protocol);

        Assert.True(ruleSet.Rules.Count >= 2);
        var loginRule = ruleSet.Rules.First(r => r.Trigger == "CS_LOGIN");
        Assert.True(loginRule.Stateful);
        Assert.Equal(2, loginRule.Responses.Count);

        var moveRule = ruleSet.Rules.First(r => r.Trigger == "CS_MOVE");
        Assert.True(moveRule.Stateful);
    }
}

