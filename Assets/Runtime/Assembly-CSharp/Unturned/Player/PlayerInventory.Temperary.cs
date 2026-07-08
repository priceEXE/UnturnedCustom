using UnityEngine;
using System.Collections.Generic;
using System;

namespace SDG.Unturned
{
    public partial class PlayerInventory
    {
        // 预定义常量字符串键,默认在游戏中添加的部分
        public const string Primary = "primary";
        public const string Secondary = "secondary";
        public const string Hands = "hands";
        public const string Backpack = "backpack";
        public const string Vest = "vest";
        public const string Shirt = "shirt";
        public const string Pants = "pants";
        public const string Storage = "storage";
        public const string Area = "area";

        //将字符串键映射到索引的字典
        private static Dictionary<string, byte> _nameToIndex = new();
        //将索引映射到字符串键的字典
        private static Dictionary<byte, string> _indexToName = new();
        private static byte _nextDynamicIndex = 64; // 动态分配从64开始，预留空间


        // 注册新存储页面（兼容注册新的服装）
        public static byte Register(string name)
        {
            if (_nameToIndex.ContainsKey(name))
                return _nameToIndex[name];
            
            byte index = _nextDynamicIndex++;
            Register(name, index);
            return index;
        }
        
        internal static void Register(string name, byte index)
        {
            _nameToIndex[name] = index;
            _indexToName[index] = name;
        }
        
        // 胶水层核心方法
        public static byte GetIndex(string name) => _nameToIndex.TryGetValue(name, out var i) ? i : byte.MaxValue;
        public static string GetName(byte index) => _indexToName.TryGetValue(index, out var n) ? n : null;
        public static bool TryGetIndex(string name, out byte index) => _nameToIndex.TryGetValue(name, out index);
        public static bool TryGetName(byte index, out string name) => _indexToName.TryGetValue(index, out name);
    }

}