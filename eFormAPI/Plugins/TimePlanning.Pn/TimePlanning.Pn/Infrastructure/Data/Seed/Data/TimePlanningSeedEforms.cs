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
namespace TimePlanning.Pn.Infrastructure.Data.Seed.Data;

using System.Collections.Generic;

public class TimePlanningSeedEforms
{
    public static List<KeyValuePair<string, List<string>>> GetForms()
    {
        List<KeyValuePair<string, List<string>>> theList = new List<KeyValuePair<string, List<string>>>();
        List<string> headers = new List<string>
        {
            "00. Arbejdstid","00.01 Arbejdstid"
        };
        KeyValuePair<string, List<string>> item = new KeyValuePair<string, List<string>>("00. Arbejdstid", headers);
        theList.Add(item);

        headers = new List<string>
        {
            "01. Info eForm","01.01 Info eForm"
        };
        item = new KeyValuePair<string, List<string>>("01. Info eForm", headers);
        theList.Add(item);

        return theList;
    }
}