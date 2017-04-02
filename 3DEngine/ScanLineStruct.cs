using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _3DEngine
{
    public struct ScanLineData
    {
        public int currentY;
        public float ndotla;
        public float ndotlb;
        public float ndotlc;
        public float ndotld;


        public float ua;
        public float ub;
        public float uc;
        public float ud;

        public float va;
        public float vb;
        public float vc;
        public float vd;
    }
}
