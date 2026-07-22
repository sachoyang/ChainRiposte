using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ChainRiposte.Game.Localization
{
    /// <summary>
    /// CSV 텍스트를 <c>행 목록(헤더 → 값)</c>으로 바꾸는 파서. 현지화 전용이 아니라 어떤 CSV에도 쓸 수 있다.
    /// 구글 시트의 <c>export?format=csv</c> 출력이 그대로 들어온다.
    /// </summary>
    public static class CsvReader
    {
        // 따옴표 밖의 콤마만 구분자로 본다 → "안녕, 반가워" 처럼 콤마가 든 문장 지원
        private const string SplitPattern = @",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
        // CRLF / LF / CR — 긴 것부터 나열해야 CRLF가 두 줄로 쪼개지지 않는다
        private const string LineSplitPattern = @"\r\n|\n\r|\n|\r";
        private const char ByteOrderMark = '﻿';

        public static List<Dictionary<string, string>> ReadString(string text)
        {
            var rows = new List<Dictionary<string, string>>();
            if (string.IsNullOrEmpty(text))
                return rows;

            string[] lines = Regex.Split(text, LineSplitPattern);
            if (lines.Length <= 1)
                return rows;

            string[] header = Regex.Split(lines[0], SplitPattern);
            for (int i = 0; i < header.Length; i++)
                header[i] = Clean(header[i]).Trim();

            for (int i = 1; i < lines.Length; i++)
            {
                string[] values = Regex.Split(lines[i], SplitPattern);
                if (values.Length == 0 || string.IsNullOrWhiteSpace(values[0]))
                    continue; // 빈 줄

                var row = new Dictionary<string, string>(header.Length);
                for (int j = 0; j < header.Length && j < values.Length; j++)
                    row[header[j]] = Clean(values[j]);

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>따옴표 정리 + BOM 제거 + <c>\n</c> 표기를 실제 줄바꿈으로.</summary>
        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // 엑셀이 UTF-8 BOM을 붙이면 첫 헤더가 '﻿Key'가 되어 Key 조회가 통째로 실패한다
            value = value.Trim(ByteOrderMark);
            value = value.Trim('"');
            value = value.Replace("\"\"", "\""); // CSV의 이스케이프된 큰따옴표

            // 셀 안의 실제 줄바꿈은 파서가 행 구분으로 오인하므로, 줄바꿈은 \n 두 글자로 적고 여기서 되돌린다.
            // (역슬래시를 통째로 지우면 이 규칙이 망가지므로 \n 만 골라서 바꾼다)
            return value.Replace("\\n", "\n");
        }
    }
}
