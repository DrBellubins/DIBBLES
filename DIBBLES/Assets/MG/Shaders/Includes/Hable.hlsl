float3 HableTonemap(float3 x)
{
    float hA = 0.15;
    float hB = 0.50;
    float hC = 0.10;
    float hD = 0.20;
    float hE = 0.02;
    float hF = 0.30;

    return ((x*(hA*x+hC*hB)+hD*hE) / (x*(hA*x+hB)+hD*hF)) - hE/hF;
}
