using Microting.TimePlanningBase.Infrastructure.Data.Entities;

namespace TimePlanning.Pn.Infrastructure.Data.Seed.Data
{
    public class TimePlanningSeedMessages
    {
        public Message[] Data => new[]
        {
            new Message
            {
                Id = 1,
                Name = "DayOff",
                DaName = "Fridag",
                EnName = "Day off",
                DeName = "Freier Tag"
            },
            new Message
            {
                Id = 2,
                Name = "Vacation",
                DaName = "Ferie",
                EnName = "Vacation",
                DeName = "Urlaub"
            },
            new Message
            {
                Id = 3,
                Name = "Sick",
                DaName = "Syg",
                EnName = "Sick",
                DeName = "Krank"
            },
            new Message
            {
                Id = 4,
                Name = "Course",
                DaName = "Kursus",
                EnName = "Course",
                DeName = "Kurs"
            },
            new Message
            {
                Id = 5,
                Name = "LeaveOfAbsence",
                DaName = "Orlov",
                EnName = "Leave of absence",
                DeName = "Urlaub"
            },
            new Message
            {
                Id = 7,
                Name = "Children1stSick",
                DaName = "Barn 1. sygedag",
                EnName = "Children 1st sick",
                DeName = "1. Krankheitstag der Kinder"
            },
            new Message
            {
                Id = 8,
                Name = "Children2ndSick",
                DaName = "Barn 2. sygedag",
                EnName = "Children 2nd sick",
                DeName = "2. Krankheitstag der Kinder"
            }
        };
    }
}