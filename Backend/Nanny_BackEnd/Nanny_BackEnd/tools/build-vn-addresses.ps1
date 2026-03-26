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

$provinceUrl = "https://raw.githubusercontent.com/madnh/hanhchinhvn/master/dist/tinh_tp.json"
$districtUrl = "https://raw.githubusercontent.com/madnh/hanhchinhvn/master/dist/quan_huyen.json"
$wardUrl = "https://raw.githubusercontent.com/madnh/hanhchinhvn/master/dist/xa_phuong.json"

Write-Host "Downloading province/district/ward data..."
$provinceMap = Get-JsonFromUrl $provinceUrl
$districtMap = Get-JsonFromUrl $districtUrl
$wardMap = Get-JsonFromUrl $wardUrl
$cityCoords = Get-CityCoordinates

$districtByProvince = @{}
foreach ($p in $districtMap.PSObject.Properties) {
    $d = $p.Value
    $provinceCode = [string]$d.parent_code
    if (-not $districtByProvince.ContainsKey($provinceCode)) {
        $districtByProvince[$provinceCode] = New-Object System.Collections.ArrayList
    }
    [void]$districtByProvince[$provinceCode].Add($d)
}

$wardByDistrict = @{}
foreach ($p in $wardMap.PSObject.Properties) {
    $w = $p.Value
    $districtCode = [string]$w.parent_code
    if (-not $wardByDistrict.ContainsKey($districtCode)) {
        $wardByDistrict[$districtCode] = New-Object System.Collections.ArrayList
    }
    [void]$wardByDistrict[$districtCode].Add($w)
}

$result = New-Object System.Collections.ArrayList
foreach ($p in $provinceMap.PSObject.Properties) {
    $province = $p.Value
    $coord = Resolve-ProvinceCoordinate $province.name $cityCoords
    $provinceCode = [string]$province.code
    $provinceDistricts = @()
    if ($districtByProvince.ContainsKey($provinceCode)) {
        $provinceDistricts = $districtByProvince[$provinceCode]
    }

    $districtOut = New-Object System.Collections.ArrayList
    foreach ($d in $provinceDistricts) {
        $districtCode = [string]$d.code
        $districtWards = @()
        if ($wardByDistrict.ContainsKey($districtCode)) {
            $districtWards = $wardByDistrict[$districtCode]
        }

        $wardOut = New-Object System.Collections.ArrayList
        foreach ($w in $districtWards) {
            [void]$wardOut.Add([ordered]@{
                code = [int]$w.code
                name = [string]$w.name
                latitude = [decimal]$coord.lat
                longitude = [decimal]$coord.lng
            })
        }

        [void]$districtOut.Add([ordered]@{
            code = [int]$d.code
            name = [string]$d.name
            latitude = [decimal]$coord.lat
            longitude = [decimal]$coord.lng
            wards = $wardOut
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
