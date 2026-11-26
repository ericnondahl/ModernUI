// The game window's content on screen, in pixels, as an ffmpeg crop (w:h:x:y): its bounds
// (points) from CoreGraphics, less its title bar and the game's menu bar, times the display's
// scale, cropped to 16:9. Usage: swift window-crop.swift <pid> <width>x<height>
import CoreGraphics
import AppKit
let pid = Int(CommandLine.arguments[1])!
let size = CommandLine.arguments[2].split(separator: "x").map { Double($0)! }
let scale = Double(NSScreen.main?.backingScaleFactor ?? 2)
let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly], kCGNullWindowID) as! [[String: Any]]
let w = windows.first { ($0[kCGWindowOwnerPID as String] as? Int) == pid && ($0[kCGWindowLayer as String] as? Int) == 3 }!
let b = w[kCGWindowBounds as String] as! [String: Double]
let contentTop = b["Y"]! + b["Height"]! - size[1]          // below the title bar
let menu = 24.0                                             // the game's menu bar, cropped off
let h = (size[1] - menu - 3) * scale
let width = (h * 16 / 9).rounded()
let x = (b["X"]! * scale + (size[0] * scale - width) / 2).rounded()
let y = ((contentTop + menu) * scale).rounded()
print("\(Int(width)):\(Int(h)):\(Int(x)):\(Int(y))")
