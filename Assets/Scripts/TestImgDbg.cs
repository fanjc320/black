using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

//namespace Assets.Scripts
namespace black_dev_tools
{
    public class TestImgDbg : MonoBehaviour
    {
        public UnityEngine.UI.Image imgDbg;
        public Texture2D texture;
        public void setImg(UnityEngine.UI.Image img)
        {
            imgDbg = img;
        }

        public void setImg(Texture2D tex)
        {
            texture = tex;
        }
    }
}
