param(
    [string]$OutputPath = "..\\Data\\vn-addresses.json"
)

$ErrorActionPreference = "Stop"

function Resolve-OutputPath([string]$pathValue) {
    if ([System.IO.Path]::IsPathRooted($pathValue)) {
        return $pathValue
    }
    $baseDir = Split-Path -Parent $PSCommandPath
    return [System.IO.Path]::GetFullPath((Join-Path $baseDir $pathValue))
}

function Get-JsonFromUrl([string]$url) {
    $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60
    if (-not $response.Content) {
        throw "No content from $url"
    }
    return $response.Content | ConvertFrom-Json
}

function Get-CityCoordinates() {
    return @{
        "Ha Noi" = @{ lat = 21.028511; lng = 105.804817 }
        "Ho Chi Minh" = @{ lat = 10.776889; lng = 106.700806 }
        "Da Nang" = @{ lat = 16.054407; lng = 108.202164 }
        "Hai Phong" = @{ lat = 20.844911; lng = 106.688084 }
        "Can Tho" = @{ lat = 10.045162; lng = 105.746857 }
        "Hue" = @{ lat = 16.463713; lng = 107.590866 }
        "Nha Trang" = @{ lat = 12.238791; lng = 109.196749 }
        "Da Lat" = @{ lat = 11.940419; lng = 108.458313 }
        "Vung Tau" = @{ lat = 10.411379; lng = 107.136002 }
        "Bien Hoa" = @{ lat = 10.957412; lng = 106.842613 }
        "Buon Ma Thuot" = @{ lat = 12.666667; lng = 108.050003 }
        "Quy Nhon" = @{ lat = 13.782967; lng = 109.219663 }
        "Ha Long" = @{ lat = 20.951851; lng = 107.074806 }
        "Viet Tri" = @{ lat = 21.322738; lng = 105.401981 }
        "Nam Dinh" = @{ lat = 20.438822; lng = 106.162105 }
        "Thai Nguyen" = @{ lat = 21.594223; lng = 105.848152 }
        "Thanh Hoa" = @{ lat = 19.806692; lng = 105.785179 }
        "Vinh" = @{ lat = 18.679585; lng = 105.681335 }
        "Phan Thiet" = @{ lat = 10.980460; lng = 108.261478 }
        "Rach Gia" = @{ lat = 10.012451; lng = 105.080917 }
    }
}

function Normalize-Key([string]$name) {
    if ([string]::IsNullOrWhiteSpace($name)) { return "" }
    $n = $name.Trim().ToLowerInvariant()
    $n = $n.Normalize([Text.NormalizationForm]::FormD)
    $sb = New-Object System.Text.StringBuilder
    foreach ($c in $n.ToCharArray()) {
        $cat = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($c)
        if ($cat -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$sb.Append($c)
        }
    }
    return $sb.ToString().Normalize([Text.NormalizationForm]::FormC).Replace("đ", "d")
}

function Resolve-ProvinceCoordinate($provinceName, $cityCoords) {
    $provinceKey = Normalize-Key $provinceName
    foreach ($k in $cityCoords.Keys) {
        $cityKey = Normalize-Key $k
        if ($provinceKey.Contains($cityKey) -or $cityKey.Contains($provinceKey)) {
            return $cityCoords[$k]
        }
    }
    return @{ lat = 16.047079; lng = 108.206230 } # VN center fallback
}

function Normalize-ProvinceName([string]$name) {
    $normalizedKey = Normalize-Key $name
    if ($normalizedKey -eq "thua thien hue") {
        return "Thanh pho Hue"
    }
    return $name
}

function Get-PropertyValue($obj, [string[]]$names) {
    if ($null -eq $obj) { return $null }
    foreach ($n in $names) {
        if ($obj.PSObject.Properties.Name -contains $n) {
            return $obj.$n
        }
    }
    return $null
}

function Resolve-ApproxDistrictCoordinate($provinceCoord, $divisionCode) {
    $seed = 0
    [void][int]::TryParse([string]$divisionCode, [ref]$seed)
    $latOffset = ((($seed % 23) - 11) * 0.0022)
    $lngOffset = ((((($seed / 23) -as [int]) % 23) - 11) * 0.0022)
    return @{
        lat = [decimal]($provinceCoord.lat + $latOffset)
        lng = [decimal]($provinceCoord.lng + $lngOffset)
    }
}

$flatDivisionUrl = "https://raw.githubusercontent.com/sunshine-tech/VietnamProvinces/main/vietnam_provinces/data/flat-divisions.json"

Write-Host "Downloading flat divisions data (post-07/2025)..."
$flatDivisions = Get-JsonFromUrl $flatDivisionUrl
$cityCoords = Get-CityCoordinates

if ($flatDivisions -is [PSCustomObject] -and ($flatDivisions.PSObject.Properties.Name -contains "data")) {
    $flatDivisions = $flatDivisions.data
}

$provinceMap = @{}
foreach ($item in $flatDivisions) {
    $provinceCode = Get-PropertyValue $item @("province_code", "provinceCode", "tinh_code", "city_code", "parent_code")
    $provinceName = Get-PropertyValue $item @("province_name", "provinceName", "province", "tinh", "city_name")
    $divisionCode = Get-PropertyValue $item @("ward_code", "wardCode", "division_code", "divisionCode", "code")
    $divisionName = Get-PropertyValue $item @("ward_name", "wardName", "division_name", "divisionName", "name")

    if ([string]::IsNullOrWhiteSpace([string]$provinceName) -or [string]::IsNullOrWhiteSpace([string]$divisionName)) {
        continue
    }

    $normalizedProvince = [string](Normalize-ProvinceName ([string]$provinceName))
    $provinceCodeKey = [string]$provinceCode
    if ([string]::IsNullOrWhiteSpace($provinceCodeKey)) {
        $provinceCodeKey = Normalize-Key $normalizedProvince
    }

    if (-not $provinceMap.ContainsKey($provinceCodeKey)) {
        $provinceMap[$provinceCodeKey] = [ordered]@{
            code = if ([string]::IsNullOrWhiteSpace([string]$provinceCode)) { 0 } else { [int]$provinceCode }
            name = $normalizedProvince
            divisions = New-Object System.Collections.ArrayList
        }
    }

    [void]$provinceMap[$provinceCodeKey].divisions.Add([ordered]@{
        code = if ([string]::IsNullOrWhiteSpace([string]$divisionCode)) { 0 } else { [int]$divisionCode }
        name = [string]$divisionName
    })
}

$result = New-Object System.Collections.ArrayList
foreach ($entry in $provinceMap.GetEnumerator() | Sort-Object { $_.Value.name }) {
    $province = $entry.Value
    $coord = Resolve-ProvinceCoordinate $province.name $cityCoords

    $districtOut = New-Object System.Collections.ArrayList
    foreach ($division in ($province.divisions | Sort-Object name)) {
        $dCoord = Resolve-ApproxDistrictCoordinate $coord $division.code
        [void]$districtOut.Add([ordered]@{
            code = [int]$division.code
            name = [string]$division.name
            latitude = [decimal]$dCoord.lat
            longitude = [decimal]$dCoord.lng
            wards = @()
        })
    }

    [void]$result.Add([ordered]@{
        code = [int]$province.code
        name = [string]$province.name
        latitude = [decimal]$coord.lat
        longitude = [decimal]$coord.lng
        districts = $districtOut
    })
}

$output = Resolve-OutputPath $OutputPath
$dir = Split-Path -Parent $output
if (-not (Test-Path $dir)) {
    New-Item -Path $dir -ItemType Directory | Out-Null
}

$result | ConvertTo-Json -Depth 10 | Set-Content -Path $output -Encoding UTF8

Write-Host "Generated: $output"
Write-Host "Province count: $($result.Count)"
