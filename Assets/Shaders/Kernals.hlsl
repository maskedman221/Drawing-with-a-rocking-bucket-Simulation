#ifndef Kernals_h
#define Kernals_h

static const float PI = 3.1415926;

float DensityKernel(float dist,float h)
{
    if(dist < h)
    {
        float scale = 15.0 / (2.0 * PI * pow(h,5));
        float v = h - dist;
        return v * v * scale;
    }

    return 0;
}

float NearDensityKernel(float dist,float h)
{
    if(dist < h)
    {
        float scale = 15.0 / (PI * pow(h,6));
        float v = h - dist;
        return v * v * v * scale;
    }

    return 0;
}

float DensityDerivative(float dist,float h)
{
    if(dist <= h)
    {
        float scale = 15.0 / (PI * pow(h,5));
        float v = h - dist;
        return -v * scale;
    }

    return 0;
}

float NearDensityDerivative(float dist,float h)
{
    if(dist <= h)
    {
        float scale = 45.0 / (PI * pow(h,6));
        float v = h - dist;
        return -v * v * scale;
    }

    return 0;
}

float Poly6Kernel(float dist,float h)
{
    if(dist < h)
    {
        float scale = 315.0 / (64.0 * PI * pow(h,9));
        float v = h * h - dist * dist;
        return scale * v * v * v;
    }

    return 0;
}

#endif