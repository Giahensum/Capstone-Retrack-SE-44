// Native packaging only: the original logo is retained byte-for-byte in assets/logo_nobg.png.
// Expo's image pipeline crops transparent side margins to the square Android icon canvas.
const { generateImageAsync } = require("@expo/image-utils");
const fs = require("node:fs/promises");
const path = require("node:path");
(async () => {
  const projectRoot = path.resolve(__dirname, "..");
  const { source } = await generateImageAsync(
    { projectRoot },
    {
      src: path.join(projectRoot, "assets/logo_nobg.png"),
      name: "retrack-icon.png",
      width: 1024,
      height: 1024,
      resizeMode: "cover",
    },
  );
  await fs.writeFile(path.join(projectRoot, "assets/retrack-icon.png"), source);
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
