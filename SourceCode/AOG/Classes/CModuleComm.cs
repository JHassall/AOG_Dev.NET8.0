namespace AOG
{
    public class CModuleComm
    {
        //copy of the mainform address
        private readonly FormGPS mf;

        //Critical Safety Properties
        public bool isOutOfBounds = true;

        // ---- Section control switches to AOG  ---------------------------------------------------------
        //PGN - 32736 - 127.249 0x7FF9
        public byte[] ss = new byte[9];

        public byte[] ssP = new byte[9];

        public int
            swHeader = 0,
            swMain = 1,
            swReserve = 2,
            swReserve2 = 3,
            swNumSections = 4,
            swOnGr0 = 5,
            swOffGr0 = 6,
            swOnGr1 = 7,
            swOffGr1 = 8;

        public int pwmDisplay = 0;
        public int pwmToolDisplay = 0;

        public double actualSteerAngleDegrees = 0;
        public int actualSteerAngleChart = 0, sensorData = -1;
        
        public double actualToolAngleDegrees = double.MaxValue;
        public int actualToolAngleChart = 0;

        //for the workswitch
        public bool isSteerWorkSwitchEnabled;

        public bool workSwitchHigh, oldWorkSwitchHigh, steerSwitchHigh, oldSteerSwitchRemote;

        // Machine data array and indices for FormModules
        public byte[] machineData = new byte[16];
        public int mdSpeedXFour = 0;
        public int mdUTurn = 1;
        public int mdTree = 2;
        public int mdHydLift = 3;

        // Auto steer data array and indices for FormModules
        public byte[] autoSteerData = new byte[16];
        public int sdHeaderHi = 0;
        public int sdHeaderLo = 1;
        public int sdSpeed = 2;
        public int sdDistanceHi = 3;
        public int sdDistanceLo = 4;
        public int sdSteerAngleHi = 5;
        public int sdSteerAngleLo = 6;
        public int sdChecksum = 7;

        public string serialRecvAutoSteerStr = "";
        public int mdHeaderHi = 8;
        public int mdHeaderLo = 9;
        public int mdSectionControlByteLo = 10;
        public int mdSectionControlByteHi = 11;

        //constructor
        public CModuleComm(FormGPS _f)
        {
            mf = _f;
        }

        //Called from "OpenGL.Designer.cs" when requied
        public void CheckWorkAndSteerSwitch()
        {
            //AutoSteerAuto button enable - Ray Bear inspired code - Thx Ray!
            if (steerSwitchHigh != oldSteerSwitchRemote)
            {
                oldSteerSwitchRemote = steerSwitchHigh;
                //steerSwith is active low
                mf.SetAutoSteerButton(!steerSwitchHigh, "");

                if (Settings.Vehicle.setF_isSteerWorkSwitchEnabled)
                {
                    mf.SetWorkState(mf.isBtnAutoSteerOn ? (Settings.Vehicle.setF_isSteerWorkSwitchManualSections ? btnStates.On : btnStates.Auto) : btnStates.Off);
                }
            }

            if (Settings.Vehicle.setF_isWorkSwitchEnabled && (oldWorkSwitchHigh != workSwitchHigh))
            {
                oldWorkSwitchHigh = workSwitchHigh;

                if (workSwitchHigh != Settings.Vehicle.setF_isWorkSwitchActiveLow)
                {
                    mf.SetWorkState(Settings.Vehicle.setF_isWorkSwitchManualSections ? btnStates.On : btnStates.Auto);
                }
                else
                {
                    mf.SetWorkState(btnStates.Off);
                }
            }

        }
    }
}