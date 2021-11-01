/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace TimePlanning.Pn.Infrastructure.Models.Planning
{
    using System;

    /// <summary>
    /// TimePlanningPlanningModel
    /// </summary>
    public class TimePlanningPlanningModel
    {
        /// <summary>
        /// Gets or sets the worker identifier.
        /// </summary>
        /// <value>The worker identifier.</value>
        public int WorkerId { get; set; }

        /// <summary>
        /// Gets or sets the week day.
        /// </summary>
        /// <value>The week day.</value>
        public int WeekDay { get; set; }

        /// <summary>
        /// Gets or sets the date.
        /// </summary>
        /// <value>The date.</value>
        public DateTime Date { get; set; }

        /// <summary>
        /// Gets or sets the plan text.
        /// </summary>
        /// <value>The plan text.</value>
        public string PlanText { get; set; }

        /// <summary>
        /// Gets or sets the plan hours.
        /// </summary>
        /// <value>The plan hours.</value>
        public double PlanHours { get; set; }

        /// <summary>
        /// Gets or sets the message identifier.
        /// </summary>
        /// <value>The message identifier.</value>
        public int? MessageId { get; set; }
    }
}