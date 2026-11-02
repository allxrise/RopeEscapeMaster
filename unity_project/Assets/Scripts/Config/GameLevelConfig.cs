using System;
using System.Collections.Generic;
using UnityEngine;

namespace RopeEscape.Config
{
    /// <summary>
    /// Exact serializable representation of GameLevelConfig from the original game.
    /// Compatible with Unity's built-in JsonUtility (zero external DLL dependencies).
    /// </summary>
    [Serializable]
    public class GameLevelConfig
    {
        public int levelId;
        public List<RopeElementData> ropeElementsDatas = new List<RopeElementData>();
        public int height = 10;
        public int width = 6;
        public List<ValidCell> validCells = new List<ValidCell>();
        public List<ValidCell> pillarCells = new List<ValidCell>();
        public List<KeyInfo> keys = new List<KeyInfo>();
        public List<ShapeInfo> shapes = new List<ShapeInfo>();
        public List<PlateInfo> plates = new List<PlateInfo>();
        public List<GridInfo> grids = new List<GridInfo>();
        public int enviromentIndex;
        public Vector3 environmentPd;
        public float levelTime;
        public DoorInfo doorInfo;
        public List<RopeElementData> rpData2 = new List<RopeElementData>();
        public List<int> dc = new List<int>();
        public List<CurtainInfo> curtains = new List<CurtainInfo>();
        public List<RodInfo> rods = new List<RodInfo>();
    }

    [Serializable]
    public class RopeElementData
    {
        public int climberXCoord;
        public int climberYCoord;
        public int flagXCoord;
        public int flagYCoord;
        public int threeXCoord;
        public int threeYCoord;
        public int lengthId = 4;
        public List<Vector3> particlePositionsData = new List<Vector3>();
        public List<Vector3> particlePositionsData2 = new List<Vector3>();
        public bool fixed1;
        public bool fixed2;
        public bool fixed3;
        public bool isThreeClimber;
        public int materialType = 1;
        public int cType1;
        public int cType2;
        public int cType3;
        public int rType1;
        public int rType2;
        public int rType3;
        public List<KeyInfo> keys = new List<KeyInfo>();
        public int g1Uid;
        public int g2Uid;
        public int g3Uid;
        public int heads;
        public int foXCoord;
        public int foYCoord;
        public int fiXCoord;
        public int fiYCoord;
        public List<Vector3> ppd3 = new List<Vector3>();
        public List<Vector3> ppd4 = new List<Vector3>();
        public bool fixed4;
        public bool fixed5;
        public int pet;
        public int iIce;
        public Vector3 rodPos;
        public int rodDir;
        public int cIndex;
    }

    [Serializable]
    public class ValidCell
    {
        public int Item1;
        public int Item2;

        public ValidCell() { }
        public ValidCell(int x, int y)
        {
            Item1 = x;
            Item2 = y;
        }
    }

    [Serializable]
    public class KeyInfo
    {
        public int x;
        public int y;
        public int type;
        public bool isKey;
    }

    [Serializable]
    public class ShapeInfo
    {
        public int shapeId;
        public int x;
        public int y;
    }

    [Serializable]
    public class PlateInfo
    {
        public float x;
        public float y;
        public float angle;
        public string name;
    }

    [Serializable]
    public class GridInfo
    {
        public float x;
        public float y;
    }

    [Serializable]
    public class DoorInfo
    {
        public float x;
        public float y;
        public float w;
        public float h;
        public int NeedKey;
        public float PosY;
    }

    [Serializable]
    public class CurtainInfo
    {
        public int x;
        public int y;
        public int w;
        public int h;
        public int count;
    }

    [Serializable]
    public class RodInfo
    {
        public float x;
        public float y;
        public int length;
        public int dir;
        public int colorType;
    }

    [Serializable]
    public class ShapeConfigEntry
    {
        public int ShapeId;
        public List<ValidCell> Points = new List<ValidCell>();
        public Vector2 Position;
    }

    [Serializable]
    public class ShapeConfigListWrapper
    {
        public List<ShapeConfigEntry> items = new List<ShapeConfigEntry>();
    }

    [Serializable]
    public class DifficultLevelConfig
    {
        public List<int> difflevels = new List<int>();
    }
}
