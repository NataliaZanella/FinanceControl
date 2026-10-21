param([switch]$PrepararSomente)

$ErrorActionPreference = 'Stop'
$raizProjeto = Split-Path $PSScriptRoot -Parent
$pastaTeste = Join-Path ([IO.Path]::GetTempPath()) ('FinanceControl-Importacao-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $pastaTeste | Out-Null
$planilha = Join-Path $pastaTeste 'importacao-sintetica.xlsx'
$banco = Join-Path $pastaTeste 'teste.db'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($planilha, [IO.Compression.ZipArchiveMode]::Create)
try {
    $arquivos = @{
        '[Content_Types].xml' = '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>'
        '_rels/.rels' = '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>'
        'xl/workbook.xml' = '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="OUTUBRO 2026" sheetId="1" r:id="rId1"/></sheets></workbook>'
        'xl/_rels/workbook.xml.rels' = '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>'
        'xl/worksheets/sheet1.xml' = @'
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
<row r="1"><c r="A1" t="inlineStr"><is><t>CONTAS</t></is></c><c r="G1" t="inlineStr"><is><t>TRABALHOS</t></is></c></row>
<row r="2"><c r="B2" t="inlineStr"><is><t>Despesa sintetica</t></is></c><c r="C2"><v>100</v></c><c r="H2" t="inlineStr"><is><t>Receita sintetica</t></is></c><c r="I2"><v>250</v></c></row>
<row r="3"><c r="B3" t="inlineStr"><is><t>Valor ausente</t></is></c></row>
<row r="4"><c r="B4" t="inlineStr"><is><t>Revisar observacao</t></is></c><c r="C4"><v>30</v></c><c r="D4" t="inlineStr"><is><t>PAGUEI - exemplo ficticio</t></is></c></row>
<row r="5"><c r="B5" t="inlineStr"><is><t>TOTAL</t></is></c><c r="C5"><v>130</v></c></row>
</sheetData></worksheet>
'@
    }
    # Abas adicionais ficticias para conferir selecao individual, lote e ano manual.
    $arquivos['[Content_Types].xml'] = $arquivos['[Content_Types].xml'].Replace('</Types>', '<Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet3.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet4.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>')
    $arquivos['xl/workbook.xml'] = $arquivos['xl/workbook.xml'].Replace('</sheets>', '<sheet name="NOVEMBRO 2026" sheetId="2" r:id="rId2"/><sheet name="JANEIRO" sheetId="3" r:id="rId3"/><sheet name="VALOR TOTAL DE DIVIDAS" sheetId="4" r:id="rId4"/></sheets>')
    $arquivos['xl/_rels/workbook.xml.rels'] = $arquivos['xl/_rels/workbook.xml.rels'].Replace('</Relationships>', '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet3.xml"/><Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet4.xml"/></Relationships>')
    $arquivos['xl/worksheets/sheet2.xml'] = $arquivos['xl/worksheets/sheet1.xml'].Replace('Despesa sintetica', 'Despesa novembro sintetica').Replace('Receita sintetica', 'Receita novembro sintetica')
    $arquivos['xl/worksheets/sheet3.xml'] = $arquivos['xl/worksheets/sheet1.xml'].Replace('Despesa sintetica', 'Despesa janeiro sintetica').Replace('Receita sintetica', 'Receita janeiro sintetica')
    $arquivos['xl/worksheets/sheet4.xml'] = '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData/></worksheet>'
    foreach ($nome in $arquivos.Keys) {
        $entrada = $zip.CreateEntry($nome)
        $writer = [IO.StreamWriter]::new($entrada.Open(), [Text.UTF8Encoding]::new($false))
        try { $writer.Write($arquivos[$nome]) } finally { $writer.Dispose() }
    }
} finally { $zip.Dispose() }

Write-Output "Planilha sintetica: $planilha"
Write-Output "Banco exclusivo do teste: $banco"
if ($PrepararSomente) { return }

# A variavel vale apenas neste processo e nos filhos; restaurada ao encerrar.
$conexaoAnterior = $env:ConnectionStrings__FinanceDb
try {
    $env:ConnectionStrings__FinanceDb = "Data Source=$banco"
    Write-Output 'Abra http://127.0.0.1:5189/importar e envie a planilha acima. Ctrl+C encerra.'
    & dotnet run --project (Join-Path $raizProjeto 'FinanceControl.Web') --artifacts-path (Join-Path $pastaTeste 'build') --no-launch-profile -- --urls http://127.0.0.1:5189 --environment Development --Logging:EventLog:LogLevel:Default None
    if ($LASTEXITCODE -ne 0) { throw "Aplicacao encerrou com codigo $LASTEXITCODE." }
} finally { $env:ConnectionStrings__FinanceDb = $conexaoAnterior }
