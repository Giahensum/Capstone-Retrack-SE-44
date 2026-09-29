// Native keys are supplied by the build environment, not committed to app.json.
module.exports = ({ config }) => {
  const mapsKey = process.env.GOOGLE_MAPS_ANDROID_API_KEY?.trim();
  const configured = Boolean(mapsKey && !mapsKey.startsWith("your_"));
  return {
    ...config,
    extra: { ...config.extra, googleMapsConfigured: configured },
    plugins: [
      ...(config.plugins || []),
      [
        "expo-location",
        {
          locationWhenInUsePermission:
            "Cho phép ReTrack sử dụng vị trí để chỉ đường đến người bán.",
        },
      ],
      ...(configured
        ? [["react-native-maps", { androidGoogleMapsApiKey: mapsKey }]]
        : []),
    ],
  };
};
