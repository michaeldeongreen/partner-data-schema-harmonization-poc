#!/bin/bash

# Script to generate additional oil & gas JSON files with varying schemas
BASE_DIR="/home/mickgreen58/git/partner-data-schema-harmonization-poc/sample-data"

# Generate additional well data files
for i in {9..35}; do
    cat > "${BASE_DIR}/well-data/well_${i}.json" << EOF
{
  "wellInfo": {
    "api": "$(printf "%02d" $((i % 50)))-$(printf "%03d" $((i * 7 % 999)))-$(printf "%05d" $((i * 123 % 99999)))",
    "wellName": "WELL_${i}_$(date +%Y)",
    "operator": "$([ $((i % 4)) -eq 0 ] && echo "SHELL" || [ $((i % 4)) -eq 1 ] && echo "BP" || [ $((i % 4)) -eq 2 ] && echo "CONOCOPHILLIPS" || echo "MARATHON")",
    "status": "$([ $((i % 3)) -eq 0 ] && echo "ACTIVE" || [ $((i % 3)) -eq 1 ] && echo "SHUT_IN" || echo "PLUGGED")"
  },
  "location": {
    "$([ $((i % 2)) -eq 0 ] && echo "latitude" || echo "lat")": $((2800 + i * 3)).$((i * 17 % 10000)),
    "$([ $((i % 2)) -eq 0 ] && echo "longitude" || echo "lng")": -$((10000 + i * 5)).$((i * 23 % 10000)),
    "$([ $((i % 2)) -eq 0 ] && echo "elevation" || echo "elev")": $((1000 + i * 10))
  },
  "$([ $((i % 2)) -eq 0 ] && echo "totalDepth" || echo "total_md")": $((10000 + i * 100)),
  "formation": "$([ $((i % 5)) -eq 0 ] && echo "Eagle Ford" || [ $((i % 5)) -eq 1 ] && echo "Bakken" || [ $((i % 5)) -eq 2 ] && echo "Permian" || [ $((i % 5)) -eq 3 ] && echo "Marcellus" || echo "Niobrara")"
}
EOF
done

# Generate additional production data files
for i in {6..35}; do
    cat > "${BASE_DIR}/production-data/prod_${i}.json" << EOF
{
  "$([ $((i % 3)) -eq 0 ] && echo "prodData" || [ $((i % 3)) -eq 1 ] && echo "production" || echo "monthlyReport")": {
    "$([ $((i % 2)) -eq 0 ] && echo "wellAPI" || echo "api_number")": "$(printf "%02d" $((i % 50)))-$(printf "%03d" $((i * 7 % 999)))-$(printf "%05d" $((i * 123 % 99999)))",
    "$([ $((i % 2)) -eq 0 ] && echo "reportDate" || echo "period")": "2024-$(printf "%02d" $((i % 12 + 1)))",
    "$([ $((i % 2)) -eq 0 ] && echo "oilVolume" || echo "oil_bbl")": $((1000 + i * 50)).$((i * 13 % 100)),
    "$([ $((i % 2)) -eq 0 ] && echo "gasVolume" || echo "gas_mcf")": $((5000 + i * 200)).$((i * 17 % 100)),
    "$([ $((i % 2)) -eq 0 ] && echo "waterVolume" || echo "water_bbl")": $((100 + i * 5)).$((i * 11 % 100)),
    "$([ $((i % 3)) -eq 0 ] && echo "tubingPressure" || [ $((i % 3)) -eq 1 ] && echo "thp" || echo "tubing_pressure_psi")": $((2000 + i * 50))
  }
}
EOF
done

# Generate additional drilling data files
for i in {4..25}; do
    cat > "${BASE_DIR}/drilling-data/drilling_${i}.json" << EOF
{
  "$([ $((i % 2)) -eq 0 ] && echo "drillingReport" || echo "dailyDrillingReport")": {
    "$([ $((i % 2)) -eq 0 ] && echo "wellId" || echo "api")": "$(printf "%02d" $((i % 50)))-$(printf "%03d" $((i * 7 % 999)))-$(printf "%05d" $((i * 123 % 99999)))",
    "$([ $((i % 2)) -eq 0 ] && echo "date" || echo "reportDate")": "2024-$(printf "%02d" $((i % 12 + 1)))-$(printf "%02d" $((i % 28 + 1)))",
    "$([ $((i % 2)) -eq 0 ] && echo "currentDepth" || echo "measured_depth")": $((5000 + i * 200)),
    "$([ $((i % 2)) -eq 0 ] && echo "dailyFootage" || echo "footage_drilled")": $((50 + i * 5)),
    "$([ $((i % 3)) -eq 0 ] && echo "rop" || [ $((i % 3)) -eq 1 ] && echo "rate_of_penetration" || echo "drilling_rate")": $((20 + i)).$((i * 7 % 10)),
    "mudWeight": $((9 + i % 5)).$((i * 3 % 10)),
    "$([ $((i % 2)) -eq 0 ] && echo "bitType" || echo "bit_size")": "$([ $((i % 3)) -eq 0 ] && echo "PDC" || [ $((i % 3)) -eq 1 ] && echo "TRICONE" || echo "IMPREGNATED")"
  }
}
EOF
done

# Generate additional seismic data files
for i in {4..20}; do
    cat > "${BASE_DIR}/seismic-data/seismic_${i}.json" << EOF
{
  "$([ $((i % 2)) -eq 0 ] && echo "seismicSurvey" || echo "geophysicalData")": {
    "$([ $((i % 2)) -eq 0 ] && echo "surveyName" || echo "project_name")": "SEISMIC_PROJECT_${i}_2024",
    "$([ $((i % 2)) -eq 0 ] && echo "client" || echo "operator")": "$([ $((i % 4)) -eq 0 ] && echo "CHEVRON" || [ $((i % 4)) -eq 1 ] && echo "EXXON" || [ $((i % 4)) -eq 2 ] && echo "SHELL" || echo "BP")",
    "$([ $((i % 2)) -eq 0 ] && echo "contractor" || echo "service_company")": "$([ $((i % 3)) -eq 0 ] && echo "CGG" || [ $((i % 3)) -eq 1 ] && echo "WesternGeco" || echo "ION")",
    "$([ $((i % 2)) -eq 0 ] && echo "surveyType" || echo "acquisition_type")": "$([ $((i % 2)) -eq 0 ] && echo "3D" || echo "2D")",
    "$([ $((i % 2)) -eq 0 ] && echo "areaSize" || echo "coverage_sq_km")": $((100 + i * 25)).$((i * 11 % 10)),
    "$([ $((i % 3)) -eq 0 ] && echo "binSize" || [ $((i % 3)) -eq 1 ] && echo "grid_spacing" || echo "spatial_sampling")": $((25 + i * 5)),
    "acquisitionDate": "2024-$(printf "%02d" $((i % 12 + 1)))-15"
  }
}
EOF
done

echo "Generated additional JSON files"