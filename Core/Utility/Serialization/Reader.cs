﻿using UnityEngine;
using System.Collections;
using System;
using System.Text;

namespace Lockstep
{
    public class Reader
    {
        public int Position {get; private set;}
        public byte[] Source {get; private set;}

        public void Initialize(byte[] Source, int StartIndex)
        {
            this.Source = Source;
            Position = StartIndex;
        }

        public void MovePosition (int amount) {
            Position += amount;
        }

        public byte ReadByte()
        {
            byte ret = Source [Position];
            Position += 1;
            return ret;
        }



        public byte[] ReadBytes(int ReadLength)
        {
            byte[] RetBytes = new byte[ReadLength];
            Array.Copy(Source, Position, RetBytes, 0, ReadLength);
            Position += ReadLength;
            return RetBytes;
        }

        public bool ReadBool()
        {
            bool ret = BitConverter.ToBoolean(Source, Position);
            Position += 1;
            return ret;
        }

        public short ReadShort()
        {
            short ret = BitConverter.ToInt16(Source, Position);
            Position += 2;
            return ret;
        }

        public ushort ReadUShort()
        {
            ushort ret = BitConverter.ToUInt16(Source, Position);
            Position += 2;
            return ret;
        }

        public int ReadInt()
        {
            int ret = BitConverter.ToInt32(Source, Position);
            Position += 4;
            return ret;
        }

        public uint ReadUInt()
        {
            uint ret = BitConverter.ToUInt32(Source, Position);
            Position += 4;
            return ret;
        }

        public long ReadLong()
        {
            long ret = BitConverter.ToInt64(Source, Position);
            Position += 8;
            return ret;
        }

        public ulong ReadULong()
        {
            ulong ret = BitConverter.ToUInt64(Source, Position);
            Position += 8;
            return ret;
        }

        public string ReadString()
        {
            ushort byteLength = BitConverter.ToUInt16(Source, Position);
            Position += 2;
            string ret = Encoding.Unicode.GetString(Source, Position, (int)byteLength);
            Position += byteLength;
            return ret;
        }
        public byte[] ReadByteArray () {
            ushort byteLength = BitConverter.ToUInt16(Source, Position);
            Position += 2;

            byte[] ret = new byte[byteLength];
            Array.Copy(Source,Position,ret,0,byteLength);

            Position += byteLength;
            return ret;

        }
    }
}