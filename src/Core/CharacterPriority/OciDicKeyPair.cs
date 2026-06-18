using Studio;

namespace HS2SandboxPlugin
{
    /// <summary>Scene character with its Studio <c>dicObjectCtrl</c> key.</summary>
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
}
