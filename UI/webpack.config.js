// Based on the official template shipped with the CS:II Modding Toolchain
// (Cities2_Data/Content/Game/.ModdingToolchain/npx-create-csii-ui-mod/template).
//
// One deliberate change from the original: the official config writes straight into
//   %CSII_USERDATAPATH%\Mods\<id>
// which is the very folder the C# build's DeployWIP target deletes and recreates. Building the
// C# side after the UI would therefore silently wipe the UI bundle.
//
// Instead we emit into ./dist, and Seety.csproj copies dist into $(OutDir) before DeployWIP runs.
// One `dotnet build` then produces the assembly and the UI together, in the right order.

const path = require("path");
const MOD = require("./mod.json");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
const { CSSPresencePlugin } = require("./tools/css-presence");
const TerserPlugin = require("terser-webpack-plugin");

const OUTPUT_DIR = path.resolve(__dirname, "dist");

// This banner is NOT decoration. The game parses it: UIModuleAsset.PostCreate reads the header
// of the .mjs through ParseModuleInfo, then calls AssetData.AddTags(m_UIModuleDependencies)
// WITHOUT a null check. Omit the "Dependencies:" line and the field stays null, so creating the
// asset throws NullReferenceException and the UI module fails to load.
//
// Every line below is part of that contract. Keep them all, even when a value is empty.
const banner = `
 * Cities: Skylines II UI Module
 *
 * Id: ${MOD.id}
 * Author: ${MOD.author}
 * Version: ${MOD.version}
 * Dependencies: ${MOD.dependencies.join(",")}
`;

module.exports = {
  mode: "production",
  stats: "errors-warnings",
  entry: {
    [MOD.id]: "./src/index.tsx",
  },
  externalsType: "window",
  externals: {
    react: "React",
    "react-dom": "ReactDOM",
    "cs2/modding": "cs2/modding",
    "cs2/api": "cs2/api",
    "cs2/bindings": "cs2/bindings",
    "cs2/l10n": "cs2/l10n",
    "cs2/ui": "cs2/ui",
    "cs2/input": "cs2/input",
    "cs2/utils": "cs2/utils",
    "cohtml/cohtml": "cohtml/cohtml",
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        use: "ts-loader",
        exclude: /node_modules/,
      },
      {
        test: /\.s?css$/,
        include: path.join(__dirname, "src"),
        use: [
          MiniCssExtractPlugin.loader,
          {
            loader: "css-loader",
            options: {
              url: true,
              importLoaders: 1,
              modules: {
                auto: true,
                exportLocalsConvention: "camelCase",
                localIdentName: "[local]_[hash:base64:3]",
              },
            },
          },
          {
            loader: "sass-loader",
            // The legacy JS API is deprecated and goes away in Dart Sass 2.0.
            options: { api: "modern" },
          },
        ],
      },
      {
        test: /\.(png|jpe?g|gif|svg)$/i,
        type: "asset/resource",
        generator: {
          filename: "images/[name][ext][query]",
        },
      },
    ],
  },
  resolve: {
    extensions: [".tsx", ".ts", ".js"],
    modules: ["node_modules", path.join(__dirname, "src")],
    alias: {
      "mod.json": path.resolve(__dirname, "mod.json"),
    },
  },
  output: {
    path: OUTPUT_DIR,
    library: {
      type: "module",
    },
    publicPath: `coui://ui-mods/`,
  },
  optimization: {
    minimize: true,
    minimizer: [
      new TerserPlugin({
        extractComments: {
          banner: () => banner,
        },
      }),
    ],
  },
  experiments: {
    outputModule: true,
  },
  plugins: [new MiniCssExtractPlugin(), new CSSPresencePlugin()],
};
