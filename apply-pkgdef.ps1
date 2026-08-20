# Elevated: manually apply StickyScroll package registration (HKLM, VS2019 32-bit hive)
$base = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\16.0\Packages\{8C3F0D52-4E6B-4A9C-B2D3-5F7A1C9E3B04}'
New-Item -Path $base -Force | Out-Null
New-ItemProperty -Path $base -Name '(default)' -Value 'StickyScroll' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $base -Name 'ID' -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $base -Name 'MinEdition' -Value 'Professional' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $base -Name 'ProductVersion' -Value '16.0' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $base -Name 'PackageLoadBehavior' -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $base -Name 'SatelliteDll' -Value '' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $base -Name 'UseInterface' -Value 0 -PropertyType DWord -Force | Out-Null

$opt = "$base\Options\StickyScroll\General"
New-Item -Path $opt -Force | Out-Null
New-ItemProperty -Path $opt -Name 'PageClass' -Value 'StickyScroll.StickyScrollOptions' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $opt -Name 'Package' -Value '{8C3F0D52-4E6B-4A9C-B2D3-5F7A1C9E3B04}' -PropertyType String -Force | Out-Null

Write-Host 'Applied.'
