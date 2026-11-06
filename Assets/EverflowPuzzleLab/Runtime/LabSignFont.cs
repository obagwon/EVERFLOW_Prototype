using UnityEngine;

namespace EverflowPuzzleLab
{
    // Reuse the installed Windows Korean font; no redistributed font or package.
    public static class LabSignFont
    {
        static Font korean;
        public static Font Get()
        {
            if(korean==null)
            {
                korean=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","맑은 고딕","Apple SD Gothic Neo","Noto Sans CJK KR"},48);
                if(korean!=null) korean.hideFlags=HideFlags.HideAndDontSave;
            }
            return korean;
        }
        public static void Apply(TextMesh text)
        {
            if(text==null) return;
            var font=Get(); if(font==null) return;
            if(text.font!=font) text.font=font;
            var renderer=text.GetComponent<Renderer>();
            if(renderer!=null && renderer.sharedMaterial!=font.material) renderer.sharedMaterial=font.material;
        }
    }
}
