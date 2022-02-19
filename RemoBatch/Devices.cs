using System;
using System.Collections.Generic;

namespace RemoBatch
{

    //Remo本体の状態など。Remo API /1/devices
    public class Devices
    {
        public string name { get; set; }
        public string id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string mac_address { get; set; }
        public string serial_number { get; set; }
        public string firmware_version { get; set; }
        public int temperature_offset { get; set; }
        public int humidity_offset { get; set; }
        public List<User> users { get; set; }
        public NewestEvents newest_events { get; set; }
    }
    public class User
    {
        public string id { get; set; }
        public string nickname { get; set; }
        public bool superuser { get; set; }
    }

    public class Te
    {
        public double val { get; set; }
        public DateTime created_at { get; set; }
    }

    public class NewestEvents
    {
        public Te te { get; set; }
    }

}
