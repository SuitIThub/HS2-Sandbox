using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>Draws stick-figure line segments via GL in a preview camera pass.</summary>
    internal static class AnimPreviewGlRenderer
    {
        private static Material? _lineMaterial;

        private static Material LineMaterial
        {
            get
            {
                if (_lineMaterial == null)
                {
                    Shader? shader = Shader.Find("Hidden/Internal-Colored");
                    _lineMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
                    _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                return _lineMaterial;
            }
        }

        public static void DrawStickFigures(Camera camera, AnimPreviewFigureDraw[] figures, int figureCount)
        {
            if (camera == null || figures == null || figureCount <= 0)
                return;

            LineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;

            for (int f = 0; f < figureCount; f++)
            {
                AnimPreviewFigureDraw figure = figures[f];
                if (figure.Joints == null || figure.Valid == null)
                    continue;

                GL.Begin(GL.LINES);
                GL.Color(figure.Color);
                for (int p = 0; p < AnimPreviewBoneSet.PairCount; p++)
                {
                    AnimPreviewBoneSet.GetPair(p, out int a, out int b);
                    if (a >= figure.JointCount || b >= figure.JointCount)
                        continue;
                    if (!figure.Valid[a] || !figure.Valid[b])
                        continue;
                    if ((figure.Joints[a] - figure.Joints[b]).sqrMagnitude < 1e-8f)
                        continue;
                    GL.Vertex(figure.Joints[a]);
                    GL.Vertex(figure.Joints[b]);
                }
                GL.End();
            }

            GL.PopMatrix();
        }
    }

    internal struct AnimPreviewFigureDraw
    {
        public Vector3[] Joints;
        public bool[] Valid;
        public int JointCount;
        public Color Color;
    }
}
