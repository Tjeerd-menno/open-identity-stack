import tailwindcssAnimate from "tailwindcss-animate";

/** @type {import('tailwindcss').Config} */
export default {
  darkMode: ["class"],
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ["'Segoe UI'", "system-ui", "-apple-system", "sans-serif"],
        heading: ["'Poppins'", "sans-serif"],
      },
      colors: {
        border: "hsl(var(--border))",
        input: "hsl(var(--input))",
        ring: "hsl(var(--ring))",
        background: "hsl(var(--background))",
        foreground: "hsl(var(--foreground))",
        primary: {
          DEFAULT: "hsl(var(--primary))",
          foreground: "hsl(var(--primary-foreground))",
        },
        secondary: {
          DEFAULT: "hsl(var(--secondary))",
          foreground: "hsl(var(--secondary-foreground))",
        },
        destructive: {
          DEFAULT: "hsl(var(--destructive))",
          foreground: "hsl(var(--destructive-foreground))",
        },
        muted: {
          DEFAULT: "hsl(var(--muted))",
          foreground: "hsl(var(--muted-foreground))",
        },
        accent: {
          DEFAULT: "hsl(var(--accent))",
          foreground: "hsl(var(--accent-foreground))",
        },
        popover: {
          DEFAULT: "hsl(var(--popover))",
          foreground: "hsl(var(--popover-foreground))",
        },
        card: {
          DEFAULT: "hsl(var(--card))",
          foreground: "hsl(var(--card-foreground))",
        },
      },
      borderRadius: {
        lg: "var(--radius)",
        md: "calc(var(--radius) - 2px)",
        sm: "calc(var(--radius) - 4px)",
      },
      boxShadow: {
        xs: "0px 1px 1px -1px rgba(0,0,0,0.20), 0px 2px 4px -2px rgba(0,0,0,0.14)",
        sm: "0px 1px 1px -1px rgba(0,0,0,0.20), 0px 2px 8px -2px rgba(0,0,0,0.16)",
        md: "0px 1px 1px -1px rgba(0,0,0,0.20), 0px 4px 12px -3px rgba(0,0,0,0.18)",
        lg: "0px 1px 1px -1px rgba(0,0,0,0.20), 0px 6px 16px -3px rgba(0,0,0,0.22)",
        button: "0px 1px 1px -1px rgba(0,0,0,0.50), 0px 3px 6px -2px rgba(0,0,0,0.40)",
      },
    },
  },
  plugins: [tailwindcssAnimate],
}
