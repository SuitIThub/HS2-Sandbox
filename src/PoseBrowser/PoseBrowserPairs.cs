using System.Collections.Generic;
using Studio;

namespace HS2SandboxPlugin
{
    /// <summary>Named pair types for net35 builds (no ValueTuple).</summary>
    public struct ZipEntryPart
    {
        public string Name;
        public byte[] Data;

        public ZipEntryPart(string name, byte[] data)
        {
            Name = name;
            Data = data;
        }
    }

    public struct OciDicKeyPair
    {
        public OCIChar Oci;
        public int DicKey;

        public OciDicKeyPair(OCIChar oci, int dicKey)
        {
            Oci = oci;
            DicKey = dicKey;
        }
    }

    public struct PoseOciNullablePair
    {
        public PoseGridItem Pose;
        public OCIChar? Character;

        public PoseOciNullablePair(PoseGridItem pose, OCIChar? character)
        {
            Pose = pose;
            Character = character;
        }
    }

    public struct PoseCharListPair
    {
        public PoseGridItem Pose;
        public List<OCIChar> Characters;

        public PoseCharListPair(PoseGridItem pose, List<OCIChar> characters)
        {
            Pose = pose;
            Characters = characters;
        }
    }

    public struct PoseOciPair
    {
        public PoseGridItem Pose;
        public OCIChar Character;

        public PoseOciPair(PoseGridItem pose, OCIChar character)
        {
            Pose = pose;
            Character = character;
        }
    }

    public struct OciLabelPair
    {
        public OCIChar Character;
        public string Label;

        public OciLabelPair(OCIChar character, string label)
        {
            Character = character;
            Label = label;
        }
    }

    public struct IntRangePair
    {
        public int Start;
        public int End;

        public IntRangePair(int start, int end)
        {
            Start = start;
            End = end;
        }
    }

    public struct PoseItemFramePair
    {
        public PoseGridItem Item;
        public int Frame;

        public PoseItemFramePair(PoseGridItem item, int frame)
        {
            Item = item;
            Frame = frame;
        }
    }

#if HS2
    internal struct OciHeelzStatePair
    {
        public OCIChar Oci;
        public HeelzCharacterState State;

        public OciHeelzStatePair(OCIChar oci, HeelzCharacterState state)
        {
            Oci = oci;
            State = state;
        }
    }
#endif
}
